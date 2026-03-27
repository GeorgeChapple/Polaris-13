using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[CustomEditor(typeof(SP_SpawnSettings))]
public class SP_SpawnSettings_Editor : Editor
{
    protected SerializedObject _s;
    protected SerializedProperty _p;

    public override void OnInspectorGUI()
    {
        SP_SpawnSettings spawner = Selection.activeObject as SP_SpawnSettings;

        DrawDefaultInspector();
        GUILayout.Label("\nPrefabs");

        if (GUILayout.Button("Add Prefab"))
        {
            spawner.spaceObjects.Add(new SP_SpawnSettings.SpaceObject());
        }
        
        List<SP_SpawnSettings.SpaceObject> newList = new List<SP_SpawnSettings.SpaceObject>();
        List<SP_SpawnSettings.SpaceObject> toDelete = new List<SP_SpawnSettings.SpaceObject>();

        foreach (SP_SpawnSettings.SpaceObject obj in spawner.spaceObjects)
        {
            SP_SpawnSettings.SpaceObject newObj;
            newObj.prefab = (GameObject)EditorGUILayout.ObjectField("Object Prefab", obj.prefab, typeof(GameObject), true);
            newObj.probability = (int)EditorGUILayout.IntField("Spawn Probability", obj.probability);
            newList.Add(newObj);
            if (GUILayout.Button("Remove Object"))
            {
                toDelete.Add(newObj);
            }
            GUILayout.Space(5);
        }

        foreach (SP_SpawnSettings.SpaceObject obj in toDelete)
        {
            newList.Remove(obj);
        }

        spawner.spaceObjects = newList;
    }
}
