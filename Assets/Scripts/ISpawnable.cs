using System;
using UnityEngine;

public interface ISpawnable
{
    Transform[] Waypoints { set; }
    event Action OnDestinationReached;
}
