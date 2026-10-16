using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// A small inspection camera for the showcase scenes. Shader functionality does
// not depend on this presenter and materials remain reusable in other projects.
public sealed class WaterShowcasePresenter : MonoBehaviour
{
    Vector3 overviewPosition;
    Quaternion overviewRotation;
    Camera view;
    Transform[] panels;
    int selection=-1;
    void Start()
    {
        view=GetComponent<Camera>();overviewPosition=transform.position;overviewRotation=transform.rotation;
        var found=new System.Collections.Generic.List<Transform>();
        foreach(var root in gameObject.scene.GetRootGameObjects())if(root.name.Length>2&&char.IsDigit(root.name[0]))found.Add(root.transform);
        found.Sort((a,b)=>string.CompareOrdinal(a.name,b.name));panels=found.ToArray();
    }
    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard=Keyboard.current;
        if(keyboard!=null)
        {
            if(keyboard.digit0Key.wasPressedThisFrame)Focus(-1);
            if(keyboard.digit1Key.wasPressedThisFrame)Focus(0);
            if(keyboard.digit2Key.wasPressedThisFrame)Focus(1);
            if(keyboard.digit3Key.wasPressedThisFrame)Focus(2);
            if(keyboard.digit4Key.wasPressedThisFrame)Focus(3);
        }
        var mouse=Mouse.current;
        if(mouse!=null)Orbit(mouse.delta.ReadValue(),mouse.scroll.ReadValue().y,mouse.rightButton.isPressed);
#elif ENABLE_LEGACY_INPUT_MANAGER
        for(int i=0;i<=4;i++)if(Input.GetKeyDown(KeyCode.Alpha0+i))Focus(i-1);
        Orbit(new Vector2(Input.GetAxisRaw("Mouse X"),-Input.GetAxisRaw("Mouse Y"))*12,Input.mouseScrollDelta.y*120,Input.GetMouseButton(1));
#endif
    }
    void Orbit(Vector2 delta,float scroll,bool rotating)
    {
        if(selection<0||panels==null)return;
        Vector3 target=panels[selection].position+Vector3.down*.6f;
        if(rotating){transform.RotateAround(target,Vector3.up,delta.x*.15f);transform.RotateAround(target,transform.right,-delta.y*.12f);}
        if(Mathf.Abs(scroll)>.1f){Vector3 offset=transform.position-target;transform.position=target+offset.normalized*Mathf.Clamp(offset.magnitude*Mathf.Exp(-scroll*.001f),3,70);}
    }
    public void Focus(int index)
    {
        if(view==null||panels==null||panels.Length==0)return;
        selection=index=Mathf.Clamp(index,-1,panels.Length-1);
        for(int i=0;i<panels.Length;i++)panels[i].gameObject.SetActive(index<0||i==index);
        if(index<0){transform.SetPositionAndRotation(overviewPosition,overviewRotation);view.fieldOfView=46;}
        else {Vector3 target=panels[index].position+Vector3.down*.6f;transform.position=target+new Vector3(10,9,-14);transform.LookAt(target);view.fieldOfView=44;}
    }
    void OnGUI()
    {
        GUI.color=new Color(.88f,.95f,.97f,.9f);
        GUILayout.BeginArea(new Rect(24,22,550,70));GUILayout.Label("WATER / FOUR IDENTITIES");
        GUILayout.BeginHorizontal();if(GUILayout.Button("0  Overview"))Focus(-1);
        string[] labels={"1  Realistic","2  Light stylized","3  Strong stylized","4  Graphic"};
        for(int i=0;i<labels.Length;i++)if(GUILayout.Button(labels[i]))Focus(i);
        GUILayout.EndHorizontal();if(selection>=0)GUILayout.Label("Right-drag to orbit  •  Scroll to zoom");GUILayout.EndArea();
    }
}
