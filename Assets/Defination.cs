using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Defination
{
    public string enqueuedTime;
    public float temperature;
    public float pressure;
    public float humidity;
    public float accelerometerZ;
    public float accelerometerY;
    public float accelerometerX;
    public float gyroscopeX;
    public float gyroscopeY;
    public float gyroscopeZ;
    public float magnetometerX;
    public float magnetometerY;
    public float magnetometerZ;
    //public string messageText;

}

public class SensorData
{
    public string enqueuedTime;
    public float temperature;
}