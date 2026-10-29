using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public sealed class WeatherRendererFeature:ScriptableRendererFeature
{
    WeatherPass pass;FoliageDepthPass foliageDepth;UnderwaterPass underwaterPass;
    public override void Create(){pass=new WeatherPass{renderPassEvent=RenderPassEvent.BeforeRenderingTransparents};foliageDepth=new FoliageDepthPass{renderPassEvent=RenderPassEvent.BeforeRenderingOpaques};underwaterPass=new UnderwaterPass{renderPassEvent=RenderPassEvent.BeforeRenderingPostProcessing};}
    public override void AddRenderPasses(ScriptableRenderer renderer,ref RenderingData renderingData)
    {
        var water=renderingData.cameraData.camera.GetComponent<LocalWaterScreen>();
        if(water!=null&&water.Blend>.001f&&water.Material!=null){water.Prepare();underwaterPass.Screen=water;renderer.EnqueuePass(underwaterPass);}
        // Prime only tree cutouts, not the entire world. Forward passes keep
        // LEqual so terrain, wildlife and grass without this pass remain valid.
        if(TerrainVideoOptions.FoliageDepthPrepass&&renderingData.cameraData.camera.cullingMask==(1<<30))renderer.EnqueuePass(foliageDepth);
        var screen=renderingData.cameraData.camera.GetComponent<LocalWeatherScreen>();if(screen==null||!screen.Ready||screen.Material==null)return;
        if(TerrainDiagnostics.NoWeather)return;
        var o=WeatherVideoOptions.Current;bool visibility=o.Visibility>0&&(screen.UseField||screen.Weather.PrecipitationMmPerHour>0||screen.Weather.Fog>0);
        if(o.Clouds==0&&o.Adaptation==0&&o.SunGlare==0&&o.SunRays==0&&screen.Flash==0&&!visibility)return;
        screen.Prepare(renderingData.cameraData.camera,(GL.GetGPUProjectionMatrix(renderingData.cameraData.GetProjectionMatrix(),true)*renderingData.cameraData.GetViewMatrix()).inverse);pass.Screen=screen;renderer.EnqueuePass(pass);
    }
    sealed class FoliageDepthPass:ScriptableRenderPass
    {
        static readonly ShaderTagId tag=new ShaderTagId("WorldGenFoliageDepth");
        sealed class Data{public RendererListHandle List;}
        public override void RecordRenderGraph(RenderGraph graph,ContextContainer frameData)
        {
            var resources=frameData.Get<UniversalResourceData>();
            var camera=frameData.Get<UniversalCameraData>();var rendering=frameData.Get<UniversalRenderingData>();var lights=frameData.Get<UniversalLightData>();
            if(!resources.activeDepthTexture.IsValid())return;
            using(var builder=graph.AddRasterRenderPass<Data>("Tree silhouettes before foliage shading",out var data))
            {
                var drawing=RenderingUtils.CreateDrawingSettings(tag,rendering,camera,lights,SortingCriteria.CommonOpaque);
                drawing.perObjectData=PerObjectData.None;
                var parameters=new RendererListParams(rendering.cullResults,drawing,new FilteringSettings(RenderQueueRange.opaque,1<<30));
                parameters.filteringSettings.batchLayerMask=uint.MaxValue;
                data.List=graph.CreateRendererList(parameters);builder.UseRendererList(data.List);
                builder.SetRenderAttachmentDepth(resources.activeDepthTexture,AccessFlags.ReadWrite);
                builder.SetRenderFunc((Data d,RasterGraphContext c)=>c.cmd.DrawRendererList(d.List));
            }
        }
    }
    sealed class UnderwaterPass:ScriptableRenderPass
    {
        public LocalWaterScreen Screen;
        sealed class Data{public TextureHandle Source,Blur,Depth;public Material Material;public int Pass;}
        public UnderwaterPass(){requiresIntermediateTexture=true;ConfigureInput(ScriptableRenderPassInput.Depth);}
        public override void RecordRenderGraph(RenderGraph graph,ContextContainer frameData)
        {
            var resources=frameData.Get<UniversalResourceData>();if(resources.isActiveTargetBackBuffer)return;Screen.DrawCount++;
            var source=resources.activeColorTexture;var desc=graph.GetTextureDesc(source);desc.msaaSamples=MSAASamples.None;desc.clearBuffer=false;
            var blur=source;
            if(Screen.BlurEyes)
            {var small=desc;small.width=Mathf.Max(1,small.width/4);small.height=Mathf.Max(1,small.height/4);small.filterMode=FilterMode.Bilinear;small.name="Underwater eye defocus";blur=graph.CreateTexture(small);Draw(graph,source,blur,default,default,Screen.Material,0);}
            desc.name="Underwater visibility";var output=graph.CreateTexture(desc);Draw(graph,source,output,blur,resources.activeDepthTexture,Screen.Material,1);resources.cameraColor=output;
        }
        static void Draw(RenderGraph graph,TextureHandle source,TextureHandle output,TextureHandle blur,TextureHandle depth,Material material,int pass)
        {
            using(var builder=graph.AddRasterRenderPass<Data>(pass==0?"Defocus unmasked underwater eyes":"Water absorption and visibility",out var data))
            {
                data.Source=source;data.Blur=blur;data.Depth=depth;data.Material=material;data.Pass=pass;builder.UseTexture(source,AccessFlags.Read);
                if(blur.IsValid())builder.UseTexture(blur,AccessFlags.Read);if(depth.IsValid())builder.UseTexture(depth,AccessFlags.Read);
                builder.SetRenderAttachment(output,0,AccessFlags.Write);builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc((Data d,RasterGraphContext c)=>{if(d.Blur.IsValid())c.cmd.SetGlobalTexture("_WaterBlurTexture",d.Blur);if(d.Depth.IsValid())c.cmd.SetGlobalTexture("_WaterDepthTexture",d.Depth);Blitter.BlitTexture(c.cmd,d.Source,new Vector4(1,1,0,0),d.Material,d.Pass);});
            }
        }
    }
    sealed class WeatherPass:ScriptableRenderPass
    {
        public LocalWeatherScreen Screen;
        sealed class Data {public TextureHandle Source,Clouds,Depth;public Material Material;public int Pass;public bool Composite;}
        public WeatherPass(){requiresIntermediateTexture=true;ConfigureInput(ScriptableRenderPassInput.Depth);}
        public override void RecordRenderGraph(RenderGraph graph,ContextContainer frameData)
        {
            var resources=frameData.Get<UniversalResourceData>();if(resources.isActiveTargetBackBuffer)return;Screen.DrawCount++;
            var source=resources.activeColorTexture;var desc=graph.GetTextureDesc(source);desc.msaaSamples=MSAASamples.None;desc.clearBuffer=false;
            int quality=WeatherVideoOptions.Current.Clouds,divisor=quality<=1?4:quality==2?3:2,cap=quality<=1?512:quality==2?768:1024;
            var small=desc;small.width=Mathf.Min(cap,Mathf.Max(1,desc.width/divisor));small.height=Mathf.Max(1,small.width*desc.height/desc.width);small.name="Weather low-resolution volume";small.filterMode=FilterMode.Bilinear;
            // The camera may use RGB-only HDR. Cloud transmittance needs its own alpha.
            small.colorFormat=UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat;
            // Forward terrain/scatter shaders do not all have depth-normal prepasses.
            // Read the actual opaque depth, not an incomplete SSAO prepass copy.
            var depth=resources.activeDepthTexture;
            var clouds=graph.CreateTexture(small);Draw(graph,source,clouds,default,depth,Screen.Material,0,false,"Weather volume");
            desc.name="Weather scene composite";var output=graph.CreateTexture(desc);Draw(graph,source,output,clouds,depth,Screen.Material,1,true,"Weather composite");resources.cameraColor=output;
        }
        static void Draw(RenderGraph graph,TextureHandle source,TextureHandle output,TextureHandle clouds,TextureHandle depth,Material material,int index,bool composite,string name)
        {
            using(var builder=graph.AddRasterRenderPass<Data>(name,out var data))
            {
                data.Source=source;data.Clouds=clouds;data.Depth=depth;data.Material=material;data.Pass=index;data.Composite=composite;
                builder.UseTexture(source,AccessFlags.Read);if(depth.IsValid())builder.UseTexture(depth,AccessFlags.Read);if(composite)builder.UseTexture(clouds,AccessFlags.Read);
                builder.SetRenderAttachment(output,0,AccessFlags.Write);builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc((Data d,RasterGraphContext c)=>{if(d.Depth.IsValid())c.cmd.SetGlobalTexture("_WeatherDepthTexture",d.Depth);if(d.Composite)c.cmd.SetGlobalTexture("_WeatherCloudTexture",d.Clouds);Blitter.BlitTexture(c.cmd,d.Source,new Vector4(1,1,0,0),d.Material,d.Pass);});
            }
        }
    }
}
