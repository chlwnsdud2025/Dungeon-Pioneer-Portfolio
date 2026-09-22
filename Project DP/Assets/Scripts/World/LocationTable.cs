using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LocationTable", menuName = "World/Location Table")]
public class LocationTable : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public LocationId id;
        [Tooltip("Build Settings에 등록된 씬 이름")]
        public string sceneName;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    public bool TryGetScene(LocationId id, out string sceneName)
    {
        foreach (Entry entry in entries)
        {
            if (entry.id != id) continue;
            sceneName = entry.sceneName;
            return !string.IsNullOrEmpty(sceneName);
        }

        sceneName = null;
        return false;
    }

    public bool TryGetLocation(string sceneName, out LocationId id)
    {
        foreach (Entry entry in entries)
        {
            if (entry.sceneName != sceneName) continue;
            id = entry.id;
            return true;
        }

        id = default;
        return false;
    }
}
