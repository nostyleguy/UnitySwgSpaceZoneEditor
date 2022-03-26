using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.Linq; 

[ExecuteInEditMode]
public class ZoneObject : MonoBehaviour
{
    public enum ZoneObjecType
    {
        NAV_POINT,
        SPAWNER,
        SPACESTATION,
        MISC
    };

    [System.Serializable]
    public class ObjVar
    {        
        public enum DataType
        {
            INT = 0,
            FLOAT = 2,
            STRING = 4,
            LIST = 5,
        };
        public string name;
        public DataType type;
        public string value;
    };
    public string strObject;
    public float fltJX;
    public float fltJY;
    public float fltJZ;
    public float fltKX;
    public float fltKY;
    public float fltKZ;
    public float fltPX;
    public float fltPY;
    public float fltPZ;
    public string strObjVars;
    public string strScripts;
    public ZoneObjecType zoneObjectType = ZoneObjecType.MISC;

    private bool scriptsHadQuotes = false;
    private bool emptyObjvarsButDollar = false;

    public List<ObjVar> objvars;
    public List<string> scripts;


    static Dictionary<ZoneObject.ZoneObjecType, Color> ObjectToColorDict;

    public void Initialize(string line)
    {
        string[] terms = line.Split('\t');
        strObject = terms[0];
        name = strObject;

        try
        {
            fltJX = float.Parse(terms[1]);
            fltJY = float.Parse(terms[2]);
            fltJZ = float.Parse(terms[3]);

            fltKX = float.Parse(terms[4]);
            fltKY = float.Parse(terms[5]);
            fltKZ = float.Parse(terms[6]);

            fltPX = float.Parse(terms[7]);
            fltPY = float.Parse(terms[8]);
            fltPZ = float.Parse(terms[9]);

            Vector3 vctK = new Vector3(fltKX, fltKY, fltKZ);
            Vector3 vctJ = new Vector3(fltJX, fltJY, fltJZ);
            Vector3 vctI = Vector3.Cross(vctJ, vctK);
            vctJ = Vector3.Cross(vctK, vctI);

            Matrix4x4 matrix = new Matrix4x4();

            matrix.m00 = vctI.x;
            matrix.m01 = vctJ.x;
            matrix.m02 = vctK.x;
            matrix.m03 = fltPX;

            matrix.m10 = vctI.y;
            matrix.m11 = vctJ.y;
            matrix.m12 = vctK.y;
            matrix.m13 = fltPY;

            matrix.m20 = vctI.z;
            matrix.m21 = vctJ.z;
            matrix.m22 = vctK.z;
            matrix.m23 = fltPZ;


            transform.localScale = matrix.ExtractScale();
            transform.rotation = matrix.ExtractRotation();
            transform.position = matrix.ExtractPosition();

            if (terms.Length >= 11)
            {
                strObjVars = terms[10];
                ParseObjvars(strObjVars);
            }

            if (terms.Length >= 12)
            {
                strScripts = terms[11];
                ParseScripts(strScripts);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("Exception parsing ZoneObject from line: '" + line + "': Exception: " + e.Message);
        }

        //transform.position = new Vector3(fltPX, fltPY, fltPZ);
        DetermineMyType(strObject);
    }

    private void ParseObjvars(string objvarStr)
    {
        if (objvarStr == "") return;

        if(objvarStr == "$|")
        {
            emptyObjvarsButDollar = true;
            return;
        }

        try
        {
            objvars = new List<ObjVar>();
            string[] terms = objvarStr.Split('|');
            for (int i = 0; i < terms.Length; i += 3)
            {
                if (terms[i] == "$")
                {
                    if(terms.Length == 1)
                    {
                        
                    }
                    break;
                }

                ObjVar o = new ObjVar();
                o.name = terms[i];
                o.type = (ObjVar.DataType)int.Parse(terms[i + 1]);
                o.value = terms[i + 2];

                objvars.Add(o);

                // Special logic
                if (o.name == "strName" || o.name == "nav_name" || o.name == "strSpawnerName")
                {
                    gameObject.name = string.Format("{0} ({1})", o.value, zoneObjectType.ToString() );
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarningFormat("Exception parsion objvars: '{0}' Exception: {1}", objvarStr, e.Message);
        }
    }

    public void PostProcessObjVars(List<ZoneObject> others)
    {
        if (objvars == null) return;

        foreach(ObjVar o in objvars)
        {
            if (o.name.StartsWith("strPatrolPoints_mangled.segment."))
            {
                int patrolNo = int.Parse(o.name.Substring("strPatrolPoints_mangled.segment.".Length));
                GameObject go = new GameObject(string.Format("Patrol Segment: {0}", patrolNo));
                go.transform.SetParent(transform);
                go.transform.localPosition = Vector3.zero;

                //Debug.Log("Made patrol segment: " + go.name);

                foreach (string point in o.value.Split(':'))
                {
                    if (point == "") continue;

                    //Debug.Log("   Should add: " + point + " to " + go.name);
                    ZoneObject zo = others.Find(x => x.GetZoneName() == point);
                    if (zo != null)
                    {
                        zo.transform.SetParent(go.transform);
                    }
                    else
                    {
                        Debug.LogWarning("Didn't find object named: " + point);
                    }
                }
            }
        }
    }

    private ObjVar GetObjvar(string name)
    {
        foreach(ObjVar o in objvars)
        {
            if(o.name == name)
            {
                return o;
            }
        }
        return null;
    }

    public string WriteLine()
    {
        return string.Format("{0}\t{1:0.000000}\t{2:0.000000}\t{3:0.000000}\t{4:0.000000}\t{5:0.000000}\t{6:0.000000}\t{7:0.00}\t{8:0.00}\t{9:0.00}\t{10}\t{11}",
            strObject, fltJX, fltJY, fltJZ, fltKX, fltKY, fltKZ, fltPX, fltPY, fltPZ, GetObjVarString(), GetScriptsStrings());
    }

    private void Update()
    {
        //fltPX = transform.position.x;
        //fltPY = transform.position.y;
        //fltPZ = transform.position.z;

        Matrix4x4 matrix = transform.localToWorldMatrix;

        Vector3 j = new Vector3(matrix.m01, matrix.m11, matrix.m21);
        Vector3 k = new Vector3(matrix.m02, matrix.m12, matrix.m22);
        Vector3 p = new Vector3(matrix.m03, matrix.m13, matrix.m23);

        fltJX = j.x;
        fltJY = j.y;
        fltJZ = j.z;

        fltKX = k.x;
        fltKY = k.y;
        fltKZ = k.z;

        fltPX = p.x;
        fltPY = p.y;
        fltPZ = p.z;
    }

    public Color GetColor()
    {
        if (ObjectToColorDict == null)
        {
            ObjectToColorDict = new Dictionary<ZoneObjecType, Color>() {
                { ZoneObjecType.MISC, Color.red },
                { ZoneObjecType.NAV_POINT, Color.blue },
                { ZoneObjecType.SPAWNER, Color.green },
                { ZoneObjecType.SPACESTATION, Color.cyan },
            };
        }
        return ObjectToColorDict[zoneObjectType];
    }
    private void ParseScripts(string scriptsStr)
    {
        try
        {
            scripts = new List<string>();

            if (scriptsStr.Length > 0)
            {
                if (scriptsStr[0] == '"')
                {
                    scriptsStr = scriptsStr.TrimStart('"').TrimEnd('"');
                    scriptsHadQuotes = true;
                    Debug.LogFormat("Scripts have quotes. Now: >{0}<", scriptsStr);
                }

                string[] terms = scriptsStr.Split(',');
                for (int i = 0; i < terms.Length; i++)
                {
                    if (terms[i].Length > 0)
                    {
                        scripts.Add(terms[i]);
                    }
                }
            }
        }
        catch ( Exception e)
        {
            Debug.LogWarningFormat("Exception parsing Scripts: {0}. Exception: {1}", scriptsStr, e.Message);
        }
    }
    private void DetermineMyType(string template)
    {
        if (template.Contains("nav_point") )
        {
            zoneObjectType = ZoneObjecType.NAV_POINT;
            transform.parent = GameObject.Find("Nav").transform;
        }
        else if (template.Contains("spawner") || template.Contains("patrol_point") )
        {
            zoneObjectType = ZoneObjecType.SPAWNER;
            transform.parent = GameObject.Find("Spawner").transform;
        }
        else if (template.Contains("spacestation"))
        {
            zoneObjectType = ZoneObjecType.SPACESTATION;
            transform.parent = GameObject.Find("SpaceStation").transform;
        }
        else
        {
            zoneObjectType = ZoneObjecType.MISC;
            transform.parent = GameObject.Find("Misc").transform;
        }
    }
    private string GetObjVarString()
    {
        if (emptyObjvarsButDollar)
        {
            return "$|";
        }

        if (objvars.Count == 0)
        {
            return "";
        }
        else
        {
            string s = "";
            foreach(ObjVar o in objvars)
            {
                s += string.Format("{0}|{1}|{2}|", o.name, ((int)o.type).ToString(), o.value);
            }
            s += "$|";
            return s;
        }
    }

    private string GetScriptsStrings()
    {
        if (scripts == null || scripts.Count == 0)
        {
            return "";
        }
        else
        {
            string output = string.Join(",", scripts.ToArray());
            return scriptsHadQuotes ? string.Format("\"{0}\"", output) : output;
        }
    }

    public string GetZoneName()
    {
        if(objvars == null)
        {
            return "";
        }
        foreach(ObjVar o in objvars)
        {
            if (o.name == "strName" || o.name == "nav_name" || o.name == "strSpawnerName")
            {
                return o.value;
            }
        }
        return "";
    }

    void OnDrawGizmos()
    {
        //Handles.BeginGUI();

        if(Selection.Contains(gameObject) )
        {
            Vector3 start = transform.position;
            foreach (ZoneObject zo in GetComponentsInChildren<ZoneObject>())
            {
                //Handles.DrawLine(start, zo.transform.position);
                Gizmos.DrawLine(start, zo.transform.position);
                start = zo.transform.position;
            }
        }
        //Handles.EndGUI();
    }

}
