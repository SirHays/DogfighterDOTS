using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

//holds ui components
[GenerateAuthoringComponent]
public class UIComponent : IComponentData
{
    public Text HealthData;
    public Text PointsData;
}
