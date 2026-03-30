using UnityEngine;
using UnityEditor;

// Script By : George Chapple
// Summary   : Displays current amount of space objects in scene

[CustomEditor(typeof(SP_SpaceManager))]
public class SP_SpaceManager_Editor : Editor
{
    public override void OnInspectorGUI()
    {
        SP_SpaceManager space = Selection.gameObjects[0].GetComponent<SP_SpaceManager>();
        if (space != null)
        {
            GUILayout.Label("Space Junk Count : " + space.debris.Count);
            GUILayout.Space(5);
            foreach (SP_Spawner spawner in space.spawners.Keys)
            {
                GUILayout.Label(spawner.settings.spawnerName + " Junk Count : " + space.spawners[spawner]);
            }
            GUILayout.Space(10);
        }
        DrawDefaultInspector();
    }
}
