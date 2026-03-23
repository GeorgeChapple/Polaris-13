using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SP_SpaceJunk))]
public class SP_SpaceJunk_Editor : Editor
{
    public override void OnInspectorGUI()
    {
        SP_SpaceJunk space = Selection.gameObjects[0].GetComponent<SP_SpaceJunk>();
        if (space != null)
        {
            GUILayout.Label("Debris Count : " + space.debris.Count);
        }
        DrawDefaultInspector();
    }
}
