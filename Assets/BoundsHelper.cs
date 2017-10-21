using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class BoundsHelper
{
    public static Bounds GetGameObjectHierarchyBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();

        return EncapsulateBounds(renderers);
    }

    public static Bounds GetGameObjectListBounds(List<GameObject> gameObjects)
    {
        Renderer[] renderers = gameObjects.Select(x => x.GetComponent<Renderer>()).ToArray();

        return EncapsulateBounds(renderers);
    }

    private static Bounds EncapsulateBounds(Renderer[] renderers)
    {
        Bounds bounds = new Bounds();

        foreach (Renderer renderer in renderers)
        {
            bounds.Encapsulate(renderer.bounds);
        }

        return bounds;
    }
}
