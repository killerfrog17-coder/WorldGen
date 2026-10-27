using System;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class PlayerCameraRigTests
{
    public static void Run()
    {
        int count=0;void Check(bool ok,string message){count++;if(!ok)throw new Exception(message);}
        var camera=new PlayerCameraRig();Check(camera.FirstPerson,"New player starts in first person");
        camera.Zoom(-1);Check(camera.Distance==2.4,"Zoom out reveals body");
        camera.ToggleLock();double distance=camera.Distance;camera.Zoom(100);camera.Zoom(-100);Check(camera.Distance==distance,"Lock preserves zoom in either direction");
        camera.ToggleLock();camera.Zoom(-100);Check(camera.Distance==12,"Maximum orbit range");
        for(int i=0;i<20;i++)camera.Zoom(1);Check(camera.FirstPerson&&camera.Distance==0,"Can return to first person");
        var pivot=new V(0,1.65,0);
        Check(PlayerCameraRig.ClearDistance(pivot,new V(0,0,-1),8,(x,z)=>0)==8,"Clear terrain preserves request");
        double clear=PlayerCameraRig.ClearDistance(pivot,new V(0,-.5,-Math.Sqrt(.75)),8,(x,z)=>0);
        Check(clear>2&&clear<3,"Camera pulls in before penetrating ground");
        clear=PlayerCameraRig.ClearDistance(pivot,new V(0,0,-1),8,(x,z)=>z< -2?2:0);
        Check(clear<2,"Camera detects intermediate ridge even with endpoint beyond it");
        string json=WorldJson.Write(new PlaySession());var legacy=WorldJson.Read<PlaySession>(json.Replace(",\"PlayerHumanVariant\":-1","").Replace("\"PlayerHumanVariant\":-1,","")
            .Replace(",\"CameraDistance\":0","").Replace("\"CameraDistance\":0,","").Replace(",\"CameraDistanceLocked\":false","").Replace("\"CameraDistanceLocked\":false,",""));
        legacy.Validate();Check(legacy.PlayerHumanVariant== -1&&legacy.CameraDistance==0&&!legacy.CameraDistanceLocked,"Old saves receive first-person/random-human defaults");
        var saved=new PlaySession{PlayerHumanVariant=4,CameraDistance=7.2,CameraDistanceLocked=true};
        var restored=WorldJson.Read<PlaySession>(WorldJson.Write(saved));restored.Validate();
        Check(restored.PlayerHumanVariant==4&&restored.CameraDistance==7.2&&restored.CameraDistanceLocked,"Save round trip retains person and view");
        Check(saved.Copy().PlayerHumanVariant==4,"Session copy retains person");
        saved.HumanRunStyle=3;Check(WorldJson.Read<PlaySession>(WorldJson.Write(saved)).HumanRunStyle==3,"Run style round trip");
        var profiles=new[]{new FaunaPrototypeProfile{slug="deer",name="Deer"}};
        foreach(int n in new[]{33,160,1000,1000000})
        {var request=FaunaSpawnCommands.Parse("/spawn deer "+n,profiles);Check(request.Error==""&&request.Count==n,"Explicit animal requests exceed former count caps");}
        foreach(double bad in new[]{double.NaN,double.PositiveInfinity,-1,13})
        {bool rejected=false;try{new PlaySession{CameraDistance=bad}.Validate();}catch(ArgumentException){rejected=true;}Check(rejected,"Reject invalid saved zoom");}
        Console.WriteLine("PASS "+count+" player camera/save assertions");
    }
}
