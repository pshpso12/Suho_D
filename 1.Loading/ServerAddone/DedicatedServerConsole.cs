using System;
using System.Threading;
using UnityEngine;

public class DedicatedServerConsole : MonoBehaviour
{
    private Thread consoleThread;
    public Insight.ServerAuthentication InAuthentication;

    private void Start()
    {
        consoleThread = new Thread(ConsoleInputListener);
        consoleThread.Start();
    }

    private void ConsoleInputListener()
    {
        while (true)
        {
            string command = Console.ReadLine();
            if (command.Equals("YumeNode_ShutDown", StringComparison.OrdinalIgnoreCase))
            {
                /*모든 Client에게 종료 명령을 보냄*/
                Debug.Log("Shutdown command received. Disconnecting all clients.");
                InAuthentication.DediSendone(true, "_", 2);
            }
            else if (command.Equals("YumeNode_Inspection", StringComparison.OrdinalIgnoreCase))
            {
                /*모든 Client에게 점검 팝업을 띄우게 함*/
                Debug.Log("Inspection command received. Inspection Log all clients.");
                InAuthentication.DediSendone(true, "_", 3);
            }
            else if (command.StartsWith("YumeNode_ChatBan_", StringComparison.OrdinalIgnoreCase))
            {
                /*_Park이면 닉네임이 Park인 유저 채팅 1시간 금지*/
                string chatBanIDString = command.Substring("YumeNode_ChatBan_".Length);
                Debug.Log($"ChatBan command received. Target ID: {chatBanIDString}");
                if (chatBanIDString != null)
                {
                    InAuthentication.DediSendone(false, chatBanIDString, 1);
                }
            }
            /*_Park이면 닉네임이 Park인 유저 게임 강제 종료*/
            else if (command.StartsWith("YumeNode_GameBan_", StringComparison.OrdinalIgnoreCase))
            {
                string gameBanIDString = command.Substring("YumeNode_GameBan_".Length);
                Debug.Log($"GameBan command received. Target ID: {gameBanIDString}");
                if(gameBanIDString != null)
                {
                    InAuthentication.DediSendone(false, gameBanIDString, 2);
                }
            }
            /*현재 접속 중인 유저 로깅*/
            else if(command.StartsWith("YumeNode_CurrentUser", StringComparison.OrdinalIgnoreCase))
            {
                int userCount = InAuthentication.registeredUsers.Count;
                Debug.Log($"Current registered users count: {userCount}");
            }
            else
            {
                Debug.Log("Unknown command.");
            }
        }
    }

    private void OnApplicationQuit()
    {
        if (consoleThread != null && consoleThread.IsAlive)
        {
            consoleThread.Abort();
        }
    }
}

/** InAuthentication의 DediSendone 입니다.**/
public void DediSendone(bool All, string GId, int BNum)
{
    if(!All)
    {
        string steamid_Get = FindPlayerSteamID(GId);
        if(steamid_Get != null)
        {
            UserContainer targetUser = registeredUsers.Find(user => user.username == steamid_Get);
            if (targetUser != null)
            {
                server.SendToClient(targetUser.connectionId , new CusDedicatedMsg()
                {
                    DediNum = BNum
                });
            }
        }
    }
    else
    {
        foreach(UserContainer user in registeredUsers)
        {
            server.SendToClient(user.connectionId, new CusDedicatedMsg()
            {
                DediNum = BNum
            });
        }
    }  
}
/** 클라이언트 측 CusDedicatedMsg() Handler와 채팅금지 기능입니다.**/
void HandleCusDediMsg(InsightNetworkMessage netMsg)
{
    CusDedicatedMsg message = netMsg.ReadMessage<CusDedicatedMsg>();
    if(message.DediNum == 1)
    {
        AddChatBanDuration(3600);
    }
    else if(message.DediNum == 2)
    {
        Application.Quit();
    }
    else if(message.DediNum == 3)
    {
        GameObject Inspection_Log = GameObject.Find("Canvas").transform.Find("Inspection_Panel")?.gameObject;
        Button Inspection_Button = Inspection_Log?.transform.Find("InspectionR_Panel/Panel/Buy_success_btn")?.GetComponent<Button>();
        if(Inspection_Log != null && Inspection_Button != null)
        {
            Inspection_Button.onClick.AddListener(() => {
                Application.Quit();
            });

            Inspection_Log.SetActive(true);
        }
    }
}
void AddChatBanDuration(float additionalDuration)
{
    chatBanDuration += additionalDuration;
    if (chatBanCoroutine == null)
    {
        chatBanCoroutine = StartCoroutine(StartChatBan());
    }
}

IEnumerator StartChatBan()
{
    ClientDataManager.Instance.ChatBan = true;
    while (chatBanDuration > 0)
    {
        yield return new WaitForSeconds(1);
        chatBanDuration -= 1;
    }
    ClientDataManager.Instance.ChatBan = false;
    chatBanCoroutine = null;
}
