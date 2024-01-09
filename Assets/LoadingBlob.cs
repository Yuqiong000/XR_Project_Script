using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
//using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Unity.VisualScripting;


public class BlobStorageManager : MonoBehaviour
{
    public GameObject Panel;
    public Button FetchDataButton; // Reference to read button
    public Button SaveButton; // Reference to save button
    public Button HistoryButton; // Reference to history button
    public Text messageText;
    public Text historyText;
    public Text disconnectedMessage;

    private Defination latestSensorData; // Store the latest data
    private float currentspeed; // Store the latest data
    private DateTime latestTimestamp = DateTime.MinValue;

    string storageAccountName = "https://streamstorge.blob.core.windows.net/";
    private string containerName = "blobcontainer";
    private string sasToken = "sp=racwdli&st=2023-12-25T12:10:52Z&se=2024-12-25T20:10:52Z&sv=2022-11-02&sr=c&sig=RU4E8aWPBqZydjkbbDDkQTzjJf192BX2%2FMrHJiMxJVM%3D";
    //private string defaultBlobUrlWithSAS = "https://streamstorge.blob.core.windows.net/blobcontainer/output/20231227/0_8c0de501c91c4e2d8b6ca8ac1bbe162b_1.json?sp=racwdli&st=2023-12-25T12:10:52Z&se=2024-12-25T20:10:52Z&sv=2022-11-02&sr=c&sig=RU4E8aWPBqZydjkbbDDkQTzjJf192BX2%2FMrHJiMxJVM%3D";
    private string blobUrlWithSAS;


    void Start()
    {
        StartCoroutine(ListBlobs());

        InvokeRepeating("FetchDataButtonClick", 0f, 10f);

        SaveButton.onClick.AddListener(SaveButtonClick);

        HistoryButton.onClick.AddListener(HistoryButtonClick);
    }

    IEnumerator ListBlobs()
    {
        string sasuri = storageAccountName + containerName + "?" + sasToken;
        Uri container_uri = new Uri(sasuri);
        var container_client = new BlobContainerClient(container_uri);
        string currentDate = DateTime.Now.ToString("yyyyMMdd");
        //string prefix = "output/20231224";
        string prefix = $"output/{currentDate}";
        bool matchingBlobFound = false;

        foreach (BlobItem blob in container_client.GetBlobs())
        {
            if (blob.Name.StartsWith(prefix))
            {
                
                blobUrlWithSAS = storageAccountName + containerName + "/" + blob.Name + "?" + sasToken;
                Debug.Log("blobUrlWithSAS:   " + blobUrlWithSAS);
                matchingBlobFound = true;

                break;
            }

        }
        if (!matchingBlobFound)
        {

            //blobUrlWithSAS = defaultBlobUrlWithSAS;
            DisconnectedMessage("Failed to connect data! Please restart the app after connecting your DevKit.", Color.red);
            yield break;
        }
        yield return null; // Return null to satisfy IEnumerator
    }



    // Method to be called when the button is clicked
    void FetchDataButtonClick()
    {
        StartCoroutine(FetchData());
    }

    void SaveButtonClick()
    {
        Debug.Log("The save button is clicked");

        if (latestSensorData != null)
        {
            Debug.Log("Saving data: " + JsonUtility.ToJson(latestSensorData));
            // Save the latest data to blob storage
            StartCoroutine(SaveData(JsonUtility.ToJson(latestSensorData)));
        }
        else
        {
            Debug.LogWarning("No data to save. Fetch data first.");
        }
    }

    // Function to handle the "History" button click

    public void HistoryButtonClick()
    {
        Debug.Log("The histiry button is clicked");

        // Clear existing data in historyText
        historyText.text = string.Empty;
        StartCoroutine(LoadHistoryData());

        StartCoroutine(FadeOutHisMessage());

    }


    IEnumerator FetchData()
    {
        using (UnityWebRequest www = UnityWebRequest.Get(blobUrlWithSAS))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError(www.error);
            }
            else
            {
                string blobData = www.downloadHandler.text;
                string[] jsonObjects = blobData.Split('\n');
                Debug.Log("blobData: " + blobData);

                // Loop through each JSON object and parse it
                foreach (string jsonObject in jsonObjects)
                {
                    if (string.IsNullOrWhiteSpace(jsonObject))
                        continue;

                    Defination sensorData = JsonUtility.FromJson<Defination>(jsonObject);
                    DateTime dateTime = DateTime.Parse(sensorData.enqueuedTime, null, System.Globalization.DateTimeStyles.RoundtripKind);

                    // Check if the current record has a later timestamp than the latest one
                    if (dateTime > latestTimestamp)
                    {
                        latestTimestamp = dateTime;
                        latestSensorData = sensorData;
                        Debug.Log("Fetching data: " + JsonUtility.ToJson(latestSensorData));
                    }
                }

                // Update UI with the latest data
                UpdateUIWithLatestData();
            }
        }
    }


    void UpdateUIWithLatestData()
    {
        if (latestSensorData != null)
        {
            // Calculate speed based on accelerometer data
            float speed = CalculateSpeed(latestSensorData);
            currentspeed = speed;
            string formattedTimestamp = latestTimestamp.ToString("yyyy-MM-dd HH:mm:ss");

            // Update the UI panel with the latest parsed data
            Panel.transform.GetChild(0).GetComponent<Text>().text = "Time                            " + "<b><size=45>" + formattedTimestamp + "</size></b>";
            Panel.transform.GetChild(1).GetComponent<Text>().text = "Temperature               " + "<b><size=45>" + latestSensorData.temperature.ToString() + " °C</size></b>";
            Panel.transform.GetChild(2).GetComponent<Text>().text = "Pressure                      " + "<b><size=45>" + latestSensorData.pressure.ToString() + " pa</size></b>";
            Panel.transform.GetChild(3).GetComponent<Text>().text = "Humidity                     " + "<b><size=45>" + latestSensorData.humidity.ToString() + " RH</size></b>";
            Panel.transform.GetChild(4).GetComponent<Text>().text = "Acceleration Z    " + "<b>" + latestSensorData.accelerometerX.ToString() + "</b>";
            Panel.transform.GetChild(5).GetComponent<Text>().text = "Acceleration Y    " + "<b>" + latestSensorData.accelerometerY.ToString() + "</b>";
            Panel.transform.GetChild(6).GetComponent<Text>().text = "Acceleration X    " + "<b>" + latestSensorData.accelerometerZ.ToString() + "</b>";
            Panel.transform.GetChild(11).GetComponent<Text>().text = "Estimated Speed         " + "<b><size=45>" + speed.ToString("0.00") + " m/s</size></b>";
            Panel.transform.GetChild(12).GetComponent<Text>().text = "Gyroscope Z      " + "<b>" + latestSensorData.gyroscopeZ.ToString() + "</b>";
            Panel.transform.GetChild(13).GetComponent<Text>().text = "Gyroscope Y      " + "<b>" + latestSensorData.gyroscopeY.ToString() + "</b>";
            Panel.transform.GetChild(14).GetComponent<Text>().text = "Gyroscope X      " + "<b>" + latestSensorData.gyroscopeX.ToString() + "</b>";
            Panel.transform.GetChild(15).GetComponent<Text>().text = "Magnetometer X,Y,Z  " + "<b><size=45>" + latestSensorData.magnetometerX.ToString() + " T,  " + latestSensorData.magnetometerY.ToString() + " T,  " + latestSensorData.magnetometerZ.ToString() + " T</size></b>";
            Panel.transform.GetChild(16).GetComponent<Text>().text = "Magnetometer Y   " + "<b><size=45>" + latestSensorData.magnetometerY.ToString() + " T</size></b>";
            Panel.transform.GetChild(17).GetComponent<Text>().text = "Magnetometer Z   " + "<b><size=45>" + latestSensorData.magnetometerZ.ToString() + " T</size></b>";
        }
        else
        {
            Debug.LogWarning("No data to update UI.");
        }
    }

    float CalculateSpeed(Defination sensorData)
    {
        // Assuming accelerometer data is in m/s^2 and the time step is 1 second
        float accelerationMagnitude = Mathf.Sqrt(
            Mathf.Pow(sensorData.accelerometerX, 2) +
            Mathf.Pow(sensorData.accelerometerY, 2) +
            Mathf.Pow(sensorData.accelerometerZ, 2)
        );

        // Simple integration to estimate speed (v = u + at, where initial velocity u is assumed to be 0)
        float speed = accelerationMagnitude * 1.0f;
        Debug.Log("speed: " + speed);
        return speed;
    }

    private string CombineData(string existingData, string newData)
    {
        if (string.IsNullOrEmpty(existingData))
        {
            // If no existing data, return the new data
            return newData;
        }
        else
        {
            // Combine existing data with new data
            return existingData + Environment.NewLine + newData;
        }
    }


    IEnumerator FetchData(string url, Action<string> callback)
    {
        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError(www.error);
            }
            else
            {
                string blobData = www.downloadHandler.text;
                callback(blobData);
            }
        }


    }

    IEnumerator SaveData(string newData)
    {
        string blobUrl = "https://streamstorge.blob.core.windows.net/blobcontainer/output/testdata.json?sp=racwd&st=2023-11-25T12:02:19Z&se=2024-11-25T20:02:19Z&sv=2022-11-02&sr=b&sig=wbROjdHjuefV3R4JRYKCA78s0ELuExiGUtjVm4QEZgU%3D";
        Debug.Log("blobUrl: " + blobUrl);

        // Fetch the existing JSON data
        string existingData = null;
        yield return FetchData(blobUrl, (data) => existingData = data);

        // Combine existing data with new data
        string combinedData = CombineData(existingData, newData);

        // Convert the combined data to bytes
        byte[] combinedDataBytes = System.Text.Encoding.UTF8.GetBytes(combinedData);

        // Create a UnityWebRequest with PUT method
        using (UnityWebRequest www = UnityWebRequest.Put(blobUrl, combinedDataBytes))
        {
            www.method = UnityWebRequest.kHttpVerbPUT;
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("x-ms-blob-type", "BlockBlob");

            Debug.Log($"Request URL: {www.url}");
            Debug.Log($"Request Method: {www.method}");
            Debug.Log($"Request Headers: {www.GetRequestHeader("Content-Type")}, {www.GetRequestHeader("x-ms-blob-type")}");
            Debug.Log($"Request Body: {combinedData}");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"HTTP Error: {www.responseCode}");
                Debug.LogError($"Error Details: {www.error}");

                // Display an error message on the UI
                ShowMessage("Failed to save data. Check console for details.", Color.red);
            }
            else
            {
                Debug.Log("Data appended successfully!");

                // Display a success message on the UI
                ShowMessage("Data appended successfully!", Color.yellow);
            }
        }
    }


    void DisconnectedMessage(string message, Color color)
    {
        disconnectedMessage.text = message;
        disconnectedMessage.color = color;

        StartCoroutine(FadeOutMessage());
    }

    void ShowMessage(string message, Color color)
    {
        messageText.text = message;
        messageText.color = color;

        StartCoroutine(FadeOutMessage());
    }

    IEnumerator FadeOutMessage()
    {
        yield return new WaitForSeconds(3f); // Wait for 3 seconds
        messageText.text = ""; // Clear the message text
    }

    IEnumerator FadeOutHisMessage()
    {
        yield return new WaitForSeconds(5f);
        historyText.text = "";
    }

    // Coroutine to load history data
    IEnumerator LoadHistoryData()
    {
        string historyData = null;
        string blobUrl = "https://streamstorge.blob.core.windows.net/blobcontainer/output/testdata.json?sp=racwd&st=2023-11-25T12:02:19Z&se=2024-11-25T20:02:19Z&sv=2022-11-02&sr=b&sig=wbROjdHjuefV3R4JRYKCA78s0ELuExiGUtjVm4QEZgU%3D";

        // Fetch data from blob storage
        yield return FetchData(blobUrl, (data) => historyData = data);

        if (!string.IsNullOrEmpty(historyData))
        {
            // Clear the existing content before displaying new data
            historyText.text = string.Empty;

            // Parse the JSON array manually
            string[] jsonRecords = historyData.Split(new[] { '}' }, StringSplitOptions.RemoveEmptyEntries);

            Dictionary<string, float> totalTemperatureByDay = new Dictionary<string, float>();
            Dictionary<string, float> totalSpeedByDay = new Dictionary<string, float>();
            Dictionary<string, int> recordCountByDay = new Dictionary<string, int>();

            foreach (string jsonRecord in jsonRecords)
            {
                // Add '}' back to each record to make them valid JSON objects
                string record = jsonRecord + '}';

                // Deserialize each record using SensorData instead of Defination
                Defination sensorData = JsonUtility.FromJson<Defination>(record);

                // Extract and use only the required fields (enqueuedTime, temperature)
                string enqueuedTime = sensorData.enqueuedTime;
                float temperature = sensorData.temperature;

                // Calculate formatted date
                string formattedDate = GetFormattedDate(enqueuedTime);

                // Accumulate temperature, speed, and record count for each day
                if (totalTemperatureByDay.ContainsKey(formattedDate))
                {
                    totalTemperatureByDay[formattedDate] += temperature;
                    totalSpeedByDay[formattedDate] += CalculateSpeed(sensorData);
                    recordCountByDay[formattedDate]++;
                }
                else
                {
                    totalTemperatureByDay[formattedDate] = temperature;
                    totalSpeedByDay[formattedDate] = CalculateSpeed(sensorData);
                    recordCountByDay[formattedDate] = 1;
                }
            }

            // Sort the keys (dates) in descending order
            List<string> sortedDates = new List<string>(totalTemperatureByDay.Keys);
            sortedDates.Sort((a, b) => DateTime.Compare(DateTime.Parse(b), DateTime.Parse(a)));

            // Display the average temperature, average speed, and record count for each day
            foreach (var date in sortedDates)
            {
                float averageTemperature = totalTemperatureByDay[date] / recordCountByDay[date];
                float averageSpeed = totalSpeedByDay[date] / recordCountByDay[date];

                Debug.Log($"Date: {date}, AvgTemperature: {averageTemperature:F2}, AvgSpeed: {averageSpeed:F2}, RecordCount: {recordCountByDay[date]}");

                // Append the information to the historyText
                //historyText.text += $"Date: {date}, AvgTemperature: {averageTemperature:F2}, AvgSpeed: {averageSpeed:F2}, RecordCount: {recordCountByDay[date]}\n";
                historyText.text += $"Date:   {date},   AvgSpeed: {averageSpeed:F2},   RecordCount:   {recordCountByDay[date]}\n";
            }


        }
        else
        {
            Debug.LogWarning("No history data found.");

        }
    }

    string GetFormattedDate(string dateTimeString)
    {
        // Parse the DateTime string and format it to display only year, month, and day
        DateTime dateTime = DateTime.Parse(dateTimeString, null, System.Globalization.DateTimeStyles.RoundtripKind);
        return dateTime.ToString("yyyy-MM-dd");
    }

    /*
    void Update()
    {
        if (latestSensorData == null)
        {
            return;
        }

        float accX = currentspeed;
        //float accX = latestSensorData.accelerometerX;
        float accZ = latestSensorData.accelerometerZ;

        if (ShouldJump(accX) && Time.time - lastJumpTime > jumpCooldown)
        {
            Debug.Log("Jump condition met totally");
            StartCoroutine(JumpCoroutine(accX));
        }

        MoveCube(accX, accZ);
    }

    void MoveCube(float accX, float accZ)
    {
        float moveX = accX * moveSpeed * Time.deltaTime;
        float moveZ = accZ * moveSpeed * Time.deltaTime;

        transform.position += new Vector3(moveX, 0, moveZ);

        float clampedX = Mathf.Clamp(transform.position.x, -scopeX, scopeX);
        float clampedY = Mathf.Clamp(transform.position.y, -scopeY, scopeY);
        float clampedZ = Mathf.Clamp(transform.position.z, -scopeZ, scopeZ);

        transform.position = new Vector3(clampedX, clampedY, clampedZ);
    }

    IEnumerator JumpCoroutine(float accX)
    {
        Debug.Log("JumpCoroutine called");

        Jump(accX);
        lastJumpTime = Time.time;
        yield return null;
    }

    bool ShouldJump(float accX)
    {
        bool shouldJump = Mathf.Abs(accX) > 1.0f;

        return shouldJump;
    }

    void Jump(float accX)
    {
        float jumpForce = accX * jumpForceMultiplier;

        Debug.Log("Applying jump force: " + jumpForce);

        cubeRigidbody.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }
    */



}





