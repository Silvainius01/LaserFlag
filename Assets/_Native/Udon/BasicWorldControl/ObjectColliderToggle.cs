
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

public class ObjectColliderToggle : UdonSharpBehaviour
{
    public Text colliderToggleText;
    public GameObject[] objects;

    private bool state = false;
    private Collider[][] allColliders;

    public void Start()
    {
        state = true;
        allColliders = new Collider[objects.Length][];
        for (int i = 0; i < objects.Length; ++i)
        {
            allColliders[i] = objects[i].GetComponentsInChildren<Collider>();
        }
    }

    public void ToggleColliders()
    {
        state = !state;
        for (int i = 0; i < objects.Length; ++i)
        {
            for (int j = 0; j < allColliders[i].Length; ++j)
            {
                allColliders[i][j].enabled = state;
            }
        }

        if (state)
        {
            colliderToggleText.text = "Colliders OFF";
        }
        else
        {
            colliderToggleText.text = "Collders ON";
        }
    }
}
