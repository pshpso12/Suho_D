using System;
using System.Net.Http;
using System.Text;
using UnityEngine;

public class LogToTelegram : MonoBehaviour
{
    private static readonly HttpClient client = new HttpClient();
    private const string telegramToken = "xx";
    private const string chatId = "xx";

    private bool isHandleStartInitialized = false;
    /*Master서버와 Game서버 구분*/
    public string WhatServer;

    public void Handle_Start()
    {
        if (!isHandleStartInitialized)
        {
            Application.logMessageReceived += HandleLog;
            isHandleStartInitialized = true;
        }
    }

    private async void HandleLog(string logString, string stackTrace, LogType type)
    {
        string message = $"{WhatServer} [{type}] {logString}\n{stackTrace}";
        await SendToTelegram(message);
    }

    private async System.Threading.Tasks.Task SendToTelegram(string message)
    {
        string url = $"https://api.telegram.org/bot{telegramToken}/sendMessage";
        var content = new StringContent(
            $"{{\"chat_id\":\"{chatId}\",\"text\":\"{message}\"}}",
            Encoding.UTF8,
            "application/json"
        );
        await client.PostAsync(url, content);
    }

    void OnDestroy()
    {
        Application.logMessageReceived -= HandleLog;
    }
}
