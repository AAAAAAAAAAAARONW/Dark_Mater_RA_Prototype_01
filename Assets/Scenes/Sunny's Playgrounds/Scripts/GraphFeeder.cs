using UnityEngine;

public class GraphFeeder : MonoBehaviour
{
    public ForestHUDLine graph;
    public float samplesPerSecond = 30f;
    private float value = 100f;

    float acc;

    void Update()
    {
        if (!graph) return;

        acc += Time.deltaTime;
        float step = 1f / samplesPerSecond;

        while (acc >= step)
        {
            acc -= step;

            //float value = GetYourValue();

            graph.AddSample(value);
        }
    }

    private void GetYourValue()
    {
        // Example: fake pingpong data
        value = 100f;
        return;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Dark Matter Hit!");
        if (!graph) return;

        DarkMatterVal dm = other.GetComponent<DarkMatterVal>();
        if (dm == null) return;

        Debug.Log("Dark Matter Hit!: dm.value");

        value = dm.value;
        return;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!graph) return;

        DarkMatterVal dm = other.GetComponent<DarkMatterVal>();
        if (dm == null) return;
        Debug.Log("Dark Matter Left!");
        value = 100;
        return;
    }
}