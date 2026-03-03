using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DarkMatterVal : MonoBehaviour
{
    public float value = 0f;   // set per object in Inspector

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }
}