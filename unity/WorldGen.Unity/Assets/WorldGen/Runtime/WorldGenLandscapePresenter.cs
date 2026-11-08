using UnityEngine;

// Explicit native visual-review flag only. Present the live game camera while
// the same landscape checks/captures run; ordinary gameplay never creates it.
public sealed class WorldGenLandscapePresenter : MonoBehaviour
{
    public Camera View;
    public RenderTexture Surface;
    void LateUpdate(){if(View!=null&&Surface!=null)View.Render();}
    void OnGUI()
    {
        if(Event.current.type==EventType.Repaint&&Surface!=null)
            GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Surface,ScaleMode.ScaleToFit,false);
    }
}
