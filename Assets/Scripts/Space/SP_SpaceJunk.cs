using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class SP_SpaceJunk : MonoBehaviour
{
    [SerializeField] private int maxDebris = 20;
    [SerializeField] private Vector2 spawnTimeRange = new Vector2(3, 6);
    private float spawnTimeLimit;
    private float spawnTimer;
    [HideInInspector] public RS_Move rocket;
    public List<GameObject> debrisPrefabs = new List<GameObject>(); 
    public List<GameObject> foundObjects = new List<GameObject>();
    public Dictionary<GameObject, Vector3> debris = new Dictionary<GameObject, Vector3>();
    public Vector3 spaceBounds = new Vector3 (20, 20, 20);

    private void Awake()
    {
        spawnTimeLimit = Random.Range(spawnTimeRange.x, spawnTimeRange.y);
        rocket = FindFirstObjectByType<RS_Move>();
    } 

    // Update is called once per frame
    void Update()
    {
        //transform.rotation = Quaternion.LookRotation(rocket.worldDirection);
        if (rocket.speed > 0.1f)
        {
            SpawnDebris();
        }
        MoveDebris();
        Collider[] colliders = Physics.OverlapBox(transform.position, spaceBounds / 2, transform.rotation);
        foundObjects.Clear();
        foreach (Collider col in colliders)
        {
            foundObjects.Add(col.gameObject);
        }
    }

    private void SpawnDebris()
    {
        if (debris.Count < maxDebris)
        {
            if (spawnTimer <= spawnTimeLimit)
            {
                spawnTimer += Time.deltaTime;
            }
            else
            {
                GameObject newDebris = Instantiate(debrisPrefabs[Random.Range(0, debrisPrefabs.Count)], new Vector3(Random.Range(spaceBounds.x / 2 * -1, spaceBounds.x / 2), Random.Range(spaceBounds.y / 2 * -1, spaceBounds.y / 2), spaceBounds.z / 2), transform.rotation);
                //newDebris.transform.SetParent(this.transform);
                newDebris.transform.eulerAngles = Vector3.back;
                debris.Add(newDebris, rocket.worldDirection);
                spawnTimeLimit = Random.Range(spawnTimeRange.x, spawnTimeRange.y);
                spawnTimer = 0;
                newDebris.GetComponent<SP_Junk>().objDirection = (Vector3.back + debris[newDebris] - rocket.worldDirection).normalized * rocket.speed;
                newDebris.GetComponent<Rigidbody>().AddForce(newDebris.GetComponent<SP_Junk>().objDirection * 100);
            }
        }
    }

    private void MoveDebris()
    {
        foreach (GameObject obj in debris.Keys)
        {
            SP_Junk junkComponent = obj.GetComponent<SP_Junk>();
            junkComponent.objDirection = (Vector3.back + debris[obj] - rocket.worldDirection).normalized * rocket.speed;
            //junkComponent.junkRotation.eulerAngles = Vector3.Lerp(junkComponent.junkRotation.eulerAngles, junkComponent.junkRotation.eulerAngles + junkComponent.junkRotationRate, Time.deltaTime);
            //obj.transform.eulerAngles = Quaternion.LookRotation(junkComponent.objDirection).eulerAngles;
            //obj.transform.localPosition = Vector3.Lerp(obj.transform.localPosition, obj.transform.localPosition + objDirection, Time.deltaTime);
            //obj.GetComponent<Rigidbody>().AddForce(objDirection * 0.001f, ForceMode.Impulse);
        }
    }

    private void OnDrawGizmos()
    {
        DrawBox(transform.position, transform.rotation, spaceBounds, Color.red);
    }

    public void DrawBox(Vector3 pos, Quaternion rot, Vector3 scale, Color c)
    {
        Matrix4x4 m = new Matrix4x4();
        m.SetTRS(pos, rot, scale);

        var point1 = m.MultiplyPoint(new Vector3(-0.5f, -0.5f, 0.5f));
        var point2 = m.MultiplyPoint(new Vector3(0.5f, -0.5f, 0.5f));
        var point3 = m.MultiplyPoint(new Vector3(0.5f, -0.5f, -0.5f));
        var point4 = m.MultiplyPoint(new Vector3(-0.5f, -0.5f, -0.5f));

        var point5 = m.MultiplyPoint(new Vector3(-0.5f, 0.5f, 0.5f));
        var point6 = m.MultiplyPoint(new Vector3(0.5f, 0.5f, 0.5f));
        var point7 = m.MultiplyPoint(new Vector3(0.5f, 0.5f, -0.5f));
        var point8 = m.MultiplyPoint(new Vector3(-0.5f, 0.5f, -0.5f));

        Debug.DrawLine(point1, point2, c);
        Debug.DrawLine(point2, point3, c);
        Debug.DrawLine(point3, point4, c);
        Debug.DrawLine(point4, point1, c);

        Debug.DrawLine(point5, point6, c);
        Debug.DrawLine(point6, point7, c);
        Debug.DrawLine(point7, point8, c);
        Debug.DrawLine(point8, point5, c);

        Debug.DrawLine(point1, point5, c);
        Debug.DrawLine(point2, point6, c);
        Debug.DrawLine(point3, point7, c);
        Debug.DrawLine(point4, point8, c);
    }
}
