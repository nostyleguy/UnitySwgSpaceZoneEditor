
using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;

public class SWGZoneEditorWindow : EditorWindow
{
    string myString = "Hello World";
    bool groupEnabled;
    bool doLabels = true;
    bool doSpawners = true;
    bool doNav = true;
    bool doMisc = true;
    bool drawPath = false;

    List<ZoneObject> ZoneObjects;

    // Add menu named "My Window" to the Window menu
    [MenuItem("SWG/SWGZoneEditorWindow")]
    static void Init()
    {
        // Get existing open window or if none, make a new one:
        SWGZoneEditorWindow window = (SWGZoneEditorWindow)EditorWindow.GetWindow(typeof(SWGZoneEditorWindow));
        window.Show();
    }

    [MenuItem("SWG/DistanceTo")]
    static void DistanceTo()
    {
        //if(Selection.gameObjects.Length == 2)
        //{
        //    Debug.Log("Distance: " + Vector3.Distance(Selection.gameObjects[0].transform.position, Selection.gameObjects[1].transform.position));
        //}
        //else
        //{
        //    Debug.Log("Need to select exactly 2 objects");
        //}
        float speed = 50;
        if(Selection.gameObjects.Length >= 1)
        {
            float distance = 0;
            float estimatedTime = 0;
            for (int i = 1; i < Selection.gameObjects.Length; i++)
            {
                distance += Vector3.Distance(Selection.gameObjects[i].transform.position, Selection.gameObjects[i - 1].transform.position);
                estimatedTime = distance / speed;
                Debug.LogFormat("Split {0} Distance: {1} (Time: {2})", i, distance, (estimatedTime / 60f));
            }
            Debug.LogFormat("Distance between {0} selected objects: {1} (Time: {2})", Selection.gameObjects.Length, distance, (estimatedTime / 60f));
        }
        else
        {
            Debug.LogWarning("Need to select at least 2 objects!");
        }
    }

    void OnGUI()
    {
        if(GUILayout.Button("Load File"))
        {
            myString = EditorUtility.OpenFilePanel("Overwrite with tab", "", "tab");
            if (myString.Length != 0)
            {
                List<string> lines = new List<string>(File.ReadLines(myString));
                
                if(lines.Count >=2)
                {
                    lines.RemoveAt(0);
                    lines.RemoveAt(0);
                    Debug.LogFormat("Read lines: {0}", lines.Count);
                    SpawnStuff(lines);
                }
                else
                {
                    Debug.LogError("File should be at least 2 lines long!");
                }
            }
        }
        myString = EditorGUILayout.TextField("Text Field", myString);

        doLabels = EditorGUILayout.Toggle("Show Labels", doLabels);
        doNav = EditorGUILayout.Toggle("Show Nav", doNav);
        doSpawners = EditorGUILayout.Toggle("Show Spawners", doSpawners);
        doMisc = EditorGUILayout.Toggle("Show Misc", doMisc);
        drawPath = EditorGUILayout.Toggle("Draw Path Between Selected Nav Points", drawPath);

        if (GUILayout.Button("Write File"))
        {
            List<string> lines = new List<string>();
            lines.Add("strObject\tfltJX\tfltJY\tfltJZ\tfltKX\tfltKY\tfltKZ\tfltPX\tfltPY\tfltPZ\tstrObjVars\tstrScripts");
            lines.Add("s\tf\tf\tf\tf\tf\tf\tf\tf\tf\tp\ts");

            List<ZoneObject> zos = new List<ZoneObject>(FindObjectsOfType<ZoneObject>());
            zos.Reverse(); // because why not?

            foreach (ZoneObject zo in zos)
            {
                lines.Add(zo.WriteLine());
            }
            string newFile = EditorUtility.SaveFilePanel("Save Zone Table", "", "test.tab", "tab");

            using (TextWriter fileTW = new StreamWriter(newFile))
            {
                fileTW.NewLine = "\n"; // Use unix line endings
                foreach(string line in lines)
                {
                    fileTW.WriteLine(line);
                }
            }

            //File.WriteAllLines(newFile, lines.ToArray());            
        }

        if (GUILayout.Button("Sort Selected Object"))
        {
            foreach (Transform t in Selection.transforms)
            {
                SortHierarchyByName(t);
            }
        }
    }

    private void SortHierarchyByName(Transform t)
    {
        List<Transform> children = t.Cast<Transform>().ToList();
        children.Sort((Transform t1, Transform t2) => { return t1.name.CompareTo(t2.name); });
        for (int i = 0; i < children.Count; ++i)
        {
            Undo.SetTransformParent(children[i], children[i].parent, "Sort Children");
            children[i].SetSiblingIndex(i);
        }
    }


    void SpawnStuff(List<string> lines)
    {
        const string PREFAB = "Prefabs/Placeholder";

        foreach(GameObject go in GameObject.FindGameObjectsWithTag("zoneObject"))
        {
            DestroyImmediate(go);
        }
        ZoneObjects = new List<ZoneObject>();
        foreach (string line in lines)
        {
            GameObject prefab = (GameObject)Resources.Load(PREFAB);
            if (prefab == null)
            {
                Debug.LogError("Couldn't load prefab: " + PREFAB);
            }
            else
            {
                try
                {

                    GameObject go = (GameObject)GameObject.Instantiate(prefab, Vector3.zero, Quaternion.identity);
                    ZoneObject script = go.AddComponent<ZoneObject>();
                    go.tag = "zoneObject";
                    script.Initialize(line);
                    ZoneObjects.Add(script);
                }
                catch (Exception e)
                {
                    Debug.LogWarningFormat("Exception parsing line!\nLine '{0}'\nException: {1}", line, e.Message);
                }

            }
        }

        foreach(ZoneObject zo in ZoneObjects)
        {
            zo.PostProcessObjVars(ZoneObjects);
        }

        SortHierarchyByName(GameObject.Find("Nav").transform);
        SortHierarchyByName(GameObject.Find("Spawner").transform);
        SortHierarchyByName(GameObject.Find("Misc").transform);
        SortHierarchyByName(GameObject.Find("SpaceStation").transform);
    }

    void OnSceneGUI(SceneView sceneView)
    {
        if (SceneView.lastActiveSceneView != null)
        {
            Handles.color = Color.black;
            Handles.DrawWireCube(Vector3.zero, Vector3.one * 16000f);
            for (int i = 0; i < ZoneObjects.Count; i++)
            {
                // ... and draw a line between them
                if (ZoneObjects[i] != null)
                {
                    //Handles.Button(GameObjects[i].transform.position, Quaternion.LookRotation(Vector3.up), 50f, 0.0f, Handles.CircleHandleCap);
                    Handles.color = ZoneObjects[i].GetColor();
                    //Vector3 camForward = SceneView.lastActiveSceneView.camera.transform.eulerAngles;
                    //Quaternion facing = Quaternion.LookRotation(camForward);

                    ZoneObject.ZoneObjecType type = ZoneObjects[i].zoneObjectType;

                    if ((type == ZoneObject.ZoneObjecType.MISC && doMisc) ||
                        (type == ZoneObject.ZoneObjecType.NAV_POINT && doNav) ||
                        (type == ZoneObject.ZoneObjecType.SPAWNER && doSpawners) ||
                        (type == ZoneObject.ZoneObjecType.SPACESTATION))
                    {
                        Handles.DrawWireCube(ZoneObjects[i].transform.position, Selection.Contains(ZoneObjects[i].gameObject) ? Vector3.one * 100f : Vector3.one * 20f);

                        if (doLabels)
                            Handles.Label(ZoneObjects[i].transform.position, ZoneObjects[i].name);
                    }
                }

            }
            //Debug.Log("Drawing Handles at: " + Vector3.zero);
            //Handles.Button(Vector3.zero, Quaternion.LookRotation(Vector3.up), 50f, 0.0f, Handles.CircleHandleCap);

            if (drawPath)
            {
                Color orig = Handles.color;
                Handles.color = Color.blue;
                if(Selection.gameObjects.Length > 1)
                {
                    List<GameObject> sorted = new List<GameObject>();
                    foreach (GameObject go in Selection.gameObjects)
                    {
                        ZoneObject zo = go.GetComponent<ZoneObject>();
                        if(zo && zo.zoneObjectType == ZoneObject.ZoneObjecType.NAV_POINT)
                        {
                            sorted.Add(go);
                        }
                    }
                    sorted  = sorted.OrderBy(o => o.name).ToList();
                    if (sorted.Count > 1)
                    {
                        for (int i = 1; i < sorted.Count; i++)
                        {
                            Handles.DrawLine(sorted[i - 1].transform.position, sorted[i].transform.position);
                        }
                    }
                }
                Handles.color = orig;
            }
            Handles.EndGUI();
            SceneView.lastActiveSceneView.Repaint();
        }
    }

    // Window has been selected
    void OnFocus()
    {
        // Remove delegate listener if it has previously
        // been assigned.
        SceneView.duringSceneGui -= this.OnSceneGUI;
        // Add (or re-add) the delegate.
        SceneView.duringSceneGui += this.OnSceneGUI;
    }

    void OnDestroy()
    {
        // When the window is destroyed, remove the delegate
        // so that it will no longer do any drawing.
        SceneView.duringSceneGui -= this.OnSceneGUI;
    }

}
