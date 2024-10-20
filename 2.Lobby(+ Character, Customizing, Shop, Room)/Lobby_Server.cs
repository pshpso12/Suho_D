using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using Steamworks;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Text.RegularExpressions;
using System;
using UnityEngine.EventSystems;
using System.Linq;
using Newtonsoft.Json;

/*동작을 쉽게 보기위해 포트폴리오에서는 서버동작과 클라이언트동작을 합쳐두었습니다.*/
/*실제로는 [TargetRpc]가 Loading_Client에서 동작합니다.*/
public class Lobby_Server : NetworkBehaviour
{
    [SerializeField] private LobbyServerScript lobbyserverscript;
    [SerializeField] private GameObject myNetworkRoomPrefab;

    /*캐시 결제 요청 금액을 받아 스팀서버에서 해당 요청이 있는지 확인*/
    [Command]
    void CmdPurcahseRe(NetworkIdentity ClientIdentity, string steamID, int Sumpurchase)
    {
        lobbyserverscript.StartCoroutine(lobbyserverscript.Purchase_Things(ClientIdentity, steamID, Sumpurchase));
    }
    /*스팀서버에서 결제 요청이 있으면 클라이언트는 결제를 확정 팝업 생성, 없다면 실패 팝업 생성*/
    [TargetRpc]
    public void RpcPurchaseReturn(NetworkConnectionToClient target, bool sucfail)
    {
        if(sucfail == true)
        {
            if(purchasethings.PurchaseCheckReal_Panel != null)
            {
                purchasethings.PurchaseCheckReal_Panel.SetActive(true);
            }
        }
        else
        {
            if(purchasethings.PurchaseCheckRealFail_Panel != null)
            {
                purchasethings.PurchaseCheckRealFail_Panel.SetActive(true);
                if(UisoundManager != null)
                    UisoundManager.PlayWarringSound();
            }
        }
    }
    /*최종 결제가 완료되면 클라이언트로 해당 orderID를 받아 DB에 재화 추가*/
    [Command]
    void SendTransactionToServer(NetworkIdentity ClientIdentity, ulong orderID)
    {
        lobbyserverscript.StartCoroutine(lobbyserverscript.GetTransactionReport(ClientIdentity, orderID));
    }
    /*재화가 정상적으로 추가 되었으면 성공 팝업, 아니라면 고객센터 문의 팝업 생성*/
    [TargetRpc]
    public void RpcFinalPurchaseReturn(NetworkConnectionToClient target, bool sucfail)
    {
        if(sucfail == true)
        {
            if(purchasethings.Purchase_Log_Success != null)
            {
                purchasethings.Purchase_Log_Success.SetActive(true);
            }
        }
        else
        {
            if(purchasethings.Purchase_Log_Fail2 != null)
            {
                purchasethings.Purchase_Log_Fail2.SetActive(true);
                if(UisoundManager != null)
                    UisoundManager.PlayWarringSound();
            }
        }
    }
    /*최종 결제가 완료되면 클라이언트의 캐시 새로고침*/
    [TargetRpc]
    public void UpdateCPCost(NetworkConnectionToClient target, int CP)
    {
        ClientDataManager.Instance.CostUpdate(CP, ClientDataManager.Instance.UserDetails.basepoint);
        /*이건 어느 씬이든 동일하기 때문에 적용 가능*/
        TMP_Text ReloadCpText = GameObject.Find("Canvas/Ui_Overone/Cp_text").GetComponent<TMP_Text>();
        if(ReloadCpText)
        {
            ReloadCpText.text = $"{ClientDataManager.Instance.UserDetails.cashpoint.ToString("N0")}";
        }
        if(UisoundManager != null)
            UisoundManager.PlayBuySound();
    }
    /*캐릭터 정보창에서 메인캐릭터 변경 시 DB에서 USER의 메인캐릭터 변경*/
    [Command]
    private void Change_MainCha(int index, NetworkIdentity ClientIdentity, string UId)
    {
        lobbyserverscript.StartCoroutine(lobbyserverscript.UpdateMaincha(index + 1, ClientIdentity, UId));
    }
    /*메인캐릭터가 정상적으로 변경되면 클라이언트에 데이터 저장, 팝업 띄움*/
    [TargetRpc]
    public void MainChaDataSendToClient(NetworkConnectionToClient target, bool sucfail, int newindex)
    {
        if(sucfail == true)
        {
            ClientDataManager.Instance.UpdateUserDetails_MainCha(newindex.ToString());
            charthings.Log_panel.SetActive(true);
            EventSystem.current.SetSelectedGameObject(charthings.Log_panel_btn.gameObject);
        }
        else
        {
            charthings.Log_panel_fail.SetActive(true);
            EventSystem.current.SetSelectedGameObject(charthings.Log_panel_fail_btn.gameObject);
            if(UisoundManager != null)
                UisoundManager.PlayWarringSound();
        }
    }
    /*새로운 방 생성 요청이 오면 방 생성*/
    [Command]
    private void CmdRoomCre(NetworkIdentity ClientIdentity, string roomName, string roomPass, string RoomPNum)
    {
        int capacity = int.Parse(RoomPNum);
        /*RoomManager를 통해 Additive로 씬이 로드 되면, 해당 씬에 방에서의 기능을 담당할 Prefeb을 생성*/
        RoomManager.Instance.CreateRoom(roomName, capacity, roomPass, (createdRoom, loadedScene) => {
            GameObject RoomGameobject = Instantiate(myNetworkRoomPrefab);
            RoomData roomData = RoomGameobject.GetComponent<RoomData>();
            if (roomData != null)
            {
                /*방의 정보를 입력*/
                roomData.RoomId = createdRoom.Id;
                roomData.PlayerIndex = 0;
                roomData.CurrnetRoomNumber = createdRoom.RoomNumber;
                roomData.CurrnetRoomName = createdRoom.RoomName;
                roomData.CurrnetMaxRoomPNumber = createdRoom.MaxRoomPNumber;
                roomData.CurrnetCurrentRoomPNumber = createdRoom.CurrentRoomPNumber;
                roomData.CurrnetPassword = createdRoom.Password;
                /*roomData.inventory는 초기화만 하고 클라이언트가 방에 입장 시 채워짐*/
                for(int i = 0; i < 4; i++)
                {
                    Item newItem = new Item
                    {
                        PlayerExist = false,
                        FakeHost = true,
                        PlayerNickname = null,
                        PlayerLevel = 0,
                        PlayerTexture = null,
                        PlayerMainCharacterID = null,
                        PlayerTopOutfitID = null,
                        PlayerBottomOutfitID = null,
                        PlayerShoesOutfitID = null,
                        PlayerAllInOneOutfitID = null,
                        Ready = false
                    };
                    roomData.inventory.Add(newItem);
                }
            }
            /*방의 기능이 있는 오브젝트를 클라이언트에도 생성*/
            NetworkServer.Spawn(RoomGameobject, ClientIdentity.connectionToClient);
            roomData.PlayerObj = ClientIdentity.gameObject;
            /*기존의 Lobby_Client를 포함한 오브젝트를 이동시켜 채팅을 나눌 수 있음*/
            SceneManager.MoveGameObjectToScene(ClientIdentity.gameObject, loadedScene);
            SceneManager.MoveGameObjectToScene(RoomGameobject, loadedScene);
            /*클라이언트들에게 Room씬으로 이동을 명령*/
            TargetChangeScene(ClientIdentity.connectionToClient, "Ingame");
            /*방이 생성되면 모든 클라이언트들에게 생성된 방이 포함된 모든 방 정보를 보내려고 했으나 이용하지 않음*/
            string roomDataJson = JsonConvert.SerializeObject(RoomManager.Instance.GetRooms());
            TargetRoomCreationComplete(ClientIdentity.connectionToClient, roomDataJson);
        });
    }
    /*클라이언트의 씬 전환*/
    [TargetRpc]
    private void TargetChangeScene(NetworkConnection target, string sceneName)
    {
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }
    /*방생성중 팝업을 닫음*/
    [TargetRpc]
    private void TargetRoomCreationComplete(NetworkConnection target, string roomDataJson)
    {
        lobbythings.Room_Cre_Panl.SetActive(false);
    }
    /*클라이언트가 방 새로고침 버튼 클릭 시 서버는 현재의 방 정보를 보내줌*/
    [Command]
    private void CmdReLoadRooms(NetworkIdentity ClientIdentity)
    {
        string roomDataJson = JsonConvert.SerializeObject(RoomManager.Instance.GetRooms());
        RpcReLoadRooms(ClientIdentity.connectionToClient, roomDataJson);
    }
    /*클라이언트는 방 정보를 받고 방 목록을 새로고침*/
    [TargetRpc]
    private void RpcReLoadRooms(NetworkConnection target, string roomDataJson)
    {
        List<Room> rooms = JsonConvert.DeserializeObject<List<Room>>(roomDataJson);
        RoomManager.Instance.rooms = rooms;
        lobbybase.UpdateRoomList();
    }
    /*방 입장 요청이 왔을 경우*/
    [Command]
    private void CmdSendRoom(NetworkIdentity ClientIdentity, Room room)
    {
        /*해당 방이 존재하는 확인*/
        Room existingRoom = RoomManager.Instance.rooms.FirstOrDefault(r => r.RoomNumber == room.RoomNumber);
        /*방이 있고, 시작안한 경우*/
        if (existingRoom != null && existingRoom.Id == room.Id && existingRoom.RoomStart == false)
        {
            /*들어갈 수 있는 자리가 있는지 확인, 방 인원이 1인 경우 true가 있어도 못 들어가므로 조건문 두개*/
            int occupiedSlots = existingRoom.PlayerSlots.Count(slot => slot == true);
            bool isSlotAvailable = existingRoom.PlayerSlots.Any(slot => slot == false) && occupiedSlots < existingRoom.MaxRoomPNumber;
            if (isSlotAvailable && occupiedSlots != 0)
            {
                /*방 입장 시 user의 index 값*/
                int playerIndex = existingRoom.PlayerSlots.FindIndex(slot => slot == false);
                /*방에 비밀번호가 없는 경우*/
                if(existingRoom.Password == "" && playerIndex != -1)
                {
                    /*방의 기능을 하는 prefeb에 기존 방의 정보를 담음*/
                    GameObject RoomGameobject = Instantiate(myNetworkRoomPrefab);
                    RoomData roomData = RoomGameobject.GetComponent<RoomData>();
                    if (roomData != null)
                    {
                        roomData.RoomId = existingRoom.Id;
                        roomData.PlayerIndex = playerIndex;
                        roomData.CurrnetRoomNumber = existingRoom.RoomNumber;
                        roomData.CurrnetRoomName = existingRoom.RoomName;
                        roomData.CurrnetMaxRoomPNumber = existingRoom.MaxRoomPNumber;
                        roomData.CurrnetCurrentRoomPNumber = existingRoom.CurrentRoomPNumber + 1;
                        roomData.CurrnetPassword = existingRoom.Password;
                        /*roomData.inventory는 초기화만 하고 클라이언트가 방에 입장 시 채워짐*/
                        for(int i = 0; i < 4; i++)
                        {
                            Item newItem = new Item
                            {
                                PlayerExist = false,
                                FakeHost = false,
                                PlayerNickname = null,
                                PlayerLevel = 0,
                                PlayerTexture = null,
                                PlayerMainCharacterID = null,
                                PlayerTopOutfitID = null,
                                PlayerBottomOutfitID = null,
                                PlayerShoesOutfitID = null,
                                PlayerAllInOneOutfitID = null,
                                Ready = false
                            };
                            roomData.inventory.Add(newItem);
                        }
                    }
                    NetworkServer.Spawn(RoomGameobject, ClientIdentity.connectionToClient);
                    roomData.PlayerObj = ClientIdentity.gameObject;
                    SceneManager.MoveGameObjectToScene(ClientIdentity.gameObject, existingRoom.RoomScene);
                    SceneManager.MoveGameObjectToScene(RoomGameobject, existingRoom.RoomScene);
                    TargetChangeScene(ClientIdentity.connectionToClient, "Ingame");
                    existingRoom.CurrentRoomPNumber += 1;
                    existingRoom.PlayerSlots[playerIndex] = true;
                }
                /*방에 비밀번호가 있는 경우 비밀번호 입력 팝업을 클라이언트에 띄움*/
                else if(existingRoom.Password != "" && playerIndex != -1)
                {
                    Enter_Room_Pass(ClientIdentity.connectionToClient, existingRoom);
                }
            }
            else
            {
                /*예외 발생 시 방 정보를 다시 클라이언트에게 방 정보를 보내주고 방 입장 실패 팝업 띄움*/
                string roomDataJson = JsonConvert.SerializeObject(RoomManager.Instance.GetRooms());
                Enter_Room_Fail(ClientIdentity.connectionToClient, roomDataJson);
            }
        }
        else
        {
            /*예외 발생 시 방 정보를 다시 클라이언트에게 방 정보를 보내주고 방 입장 실패 팝업 띄움*/
            string roomDataJson = JsonConvert.SerializeObject(RoomManager.Instance.GetRooms());
            Enter_Room_Fail(ClientIdentity.connectionToClient, roomDataJson);
        }
    }
    /*비밀번호 팝업*/
    [TargetRpc]
    private void Enter_Room_Pass(NetworkConnection target, Room room)
    {
        lobbythings.Room_Enter_Panl.SetActive(false);
        lobbythings.Room_Enter_Withpass_Panl.SetActive(true);
        lobbythings.Room_Enter_Withpass_Button.onClick.RemoveAllListeners();
        lobbythings.Room_Enter_Withpass_Button.onClick.AddListener(() => OnSubmitRoomPasswordClicked(room));
    }
    /*비밀번호를 방 정보와 받을 경우(비밀번호 확인 제외 기존 방 입장과 동작은 동일함 - 통합 필요)*/
    [Command]
    private void CmdSubmitRoomPass(NetworkIdentity ClientIdentity, string password, Room room)
    {
        Room existingRoom = RoomManager.Instance.rooms.FirstOrDefault(r => r.RoomNumber == room.RoomNumber);
        if (existingRoom != null && existingRoom.Id == room.Id && existingRoom.RoomStart == false)
        {
            int occupiedSlots = existingRoom.PlayerSlots.Count(slot => slot == true);
            bool isSlotAvailable = existingRoom.PlayerSlots.Any(slot => slot == false) && occupiedSlots < existingRoom.MaxRoomPNumber;
            if (isSlotAvailable && occupiedSlots != 0)
            {
                int playerIndex = existingRoom.PlayerSlots.FindIndex(slot => slot == false);
                if(existingRoom.Password == password && playerIndex != -1)
                {
                    GameObject RoomGameobject = Instantiate(myNetworkRoomPrefab);
                    RoomData roomData = RoomGameobject.GetComponent<RoomData>();
                    if (roomData != null)
                    {
                        roomData.RoomId = existingRoom.Id;
                        roomData.PlayerIndex = playerIndex;
                        roomData.CurrnetRoomNumber = existingRoom.RoomNumber;
                        roomData.CurrnetRoomName = existingRoom.RoomName;
                        roomData.CurrnetMaxRoomPNumber = existingRoom.MaxRoomPNumber;
                        roomData.CurrnetCurrentRoomPNumber = existingRoom.CurrentRoomPNumber + 1;
                        roomData.CurrnetPassword = existingRoom.Password;
                        for(int i = 0; i < 4; i++)
                        {
                            Item newItem = new Item
                            {
                                PlayerExist = false,
                                FakeHost = false,
                                PlayerNickname = null,
                                PlayerLevel = 0,
                                PlayerTexture = null,
                                PlayerMainCharacterID = null,
                                PlayerTopOutfitID = null,
                                PlayerBottomOutfitID = null,
                                PlayerShoesOutfitID = null,
                                PlayerAllInOneOutfitID = null,
                                Ready = false
                            };
                            roomData.inventory.Add(newItem);
                        }
                    }
                    NetworkServer.Spawn(RoomGameobject, ClientIdentity.connectionToClient);
                    roomData.PlayerObj = ClientIdentity.gameObject;
                    SceneManager.MoveGameObjectToScene(ClientIdentity.gameObject, existingRoom.RoomScene);
                    SceneManager.MoveGameObjectToScene(RoomGameobject, existingRoom.RoomScene);
                    TargetChangeScene(ClientIdentity.connectionToClient, "Ingame");
                    existingRoom.CurrentRoomPNumber += 1;
                    existingRoom.PlayerSlots[playerIndex] = true;
                }
                else if(existingRoom.Password != password && playerIndex != -1)
                {
                    Enter_Room_Pass_Fail(ClientIdentity.connectionToClient);
                }
            }
            else
            {
                string roomDataJson = JsonConvert.SerializeObject(RoomManager.Instance.GetRooms());
                Enter_PassRoom_Fail(ClientIdentity.connectionToClient, roomDataJson);
            }
        }
        else
        {
            string roomDataJson = JsonConvert.SerializeObject(RoomManager.Instance.GetRooms());
            Enter_PassRoom_Fail(ClientIdentity.connectionToClient, roomDataJson);
        }
    }
    /*방 입장 실패 시 팝업과 방 목록 새로고침*/
    [TargetRpc]
    private void Enter_Room_Fail(NetworkConnection target, string roomDataJson)
    {
        lobbythings.Room_Enter_Panl.SetActive(false);
        lobbythings.Room_Enter_Error_Panl.SetActive(true);
        List<Room> rooms = JsonConvert.DeserializeObject<List<Room>>(roomDataJson);
        RoomManager.Instance.rooms = rooms;
        lobbybase.UpdateRoomList();
        if(UisoundManager != null)
            UisoundManager.PlayWarringSound();
    }
    /*방 입장 실패 시 팝업과 방 목록 새로고침*/
    [TargetRpc]
    private void Enter_PassRoom_Fail(NetworkConnection target, string roomDataJson)
    {
        lobbythings.Room_Enter_Withpass_Panl.SetActive(false);
        lobbythings.Room_Enter_Panl.SetActive(false);
        lobbythings.Room_Enter_Error_Panl.SetActive(true);
        List<Room> rooms = JsonConvert.DeserializeObject<List<Room>>(roomDataJson);
        RoomManager.Instance.rooms = rooms;
        lobbybase.UpdateRoomList();
        if(UisoundManager != null)
            UisoundManager.PlayWarringSound();
    }
    /*방 입장 실패 시 팝업과 방 목록 새로고침*/
    [TargetRpc]
    private void Enter_Room_Pass_Fail(NetworkConnection target)
    {
        lobbythings.Room_Enter_Panl.SetActive(false);
        lobbythings.Room_Enter_Error_Passwrong.SetActive(true);
        if(UisoundManager != null)
            UisoundManager.PlayWarringSound();
    }
    /*강제퇴장 당했을 경우*/
    [TargetRpc]
    public void TargetActivateRoomExitForced(NetworkConnection target)
    {
        StartCoroutine(WaitForSceneAndActivate());
    }
    /*일반 메시지를 서버가 받으면 시간과 닉네임, 메시지를 위치에 맞춰 클라이언트에 배포*/
    [Command]
    private void CmdSendMessage(string message, string nickname)
    {
        string currentTime = DateTime.Now.ToString("HH:mm");
        RpcHandleMessage($"[{currentTime}] {nickname}: {message}");
    }
    /*귓속말을 서버가 받은 경우*/
    [Command]
    private void CmdSendWhisper(string target, string message, string nickname)
    {
        string currentTime = DateTime.Now.ToString("HH:mm");
        
        NetworkConnectionToClient targetConnection = FindPlayerConnection(target);
        /*귓속말 대상이 MasterServer에 있는 경우*/
        if (targetConnection != null)
        {
            if(target != nickname)
            {
                /*귓속말을 보낸 유저에게 "[00:19] 당신이 park에게: message"를 채팅창에 출력할 수 있게 합니다.*/
                TargetReceiveWhisper(connectionToClient, $"<color=#FFDA2F>[{currentTime}] 당신이 <b>{target}</b>에게: {message}</color>");
                foreach (var player in targetConnection.owned)
                {
                    /*귓속말을 받는 대상은 "[00:19] park이 당신에게: message"를 채팅창에 출력할 수 있게 합니다.*/
                    player.GetComponent<Lobby_Server>()?.TargetReceiveWhisper(targetConnection, $"<color=#FFDA2F>[{currentTime}] <b>{nickname}</b>이 당신에게: {message}</color>");
                }
            }
            else if(target == nickname)
                /*귓속말 대상이 자신일 경우, "자신에게 귓속말을 보낼 수 없습니다."를 출력할 수 있게 합니다.*/
                TargetReceiveWhisper(connectionToClient, $"<color=#FF3B48>[{currentTime}] 자신에게 귓속말을 보낼 수 없습니다.</color>");
        }
        /*귓속말 대상이 MasterServer에 없는 경우*/
        else
        {
            if(target != nickname)
            {
                /*해당 유저가 GameServer에는 있는지 확인*/
                string steamid_Get = FindPlayerSteamID(target);
                if(steamid_Get != null)
                {
                    var ChatRegi = FindObjectOfType<Insight.ChatServer>();
                    if (ChatRegi != null)
                    {
                         /*접속해 있다면 해당 유저에게 메시지 전송, 메시지를 보낸 유저에게도 출력*/
                        TargetReceiveWhisper(connectionToClient, $"<color=#FFDA2F>[{currentTime}] 당신이 <b>{target}</b>에게: {message}</color>");
                        ChatRegi.SendChatToGame(steamid_Get, $"<color=#FFDA2F>[{currentTime}] <b>{nickname}</b>이 당신에게: {message}</color>");
                    }
                    else
                        /*접속해 있지만 못 보낸 경우 실패 출력*/
                        TargetReceiveWhisper(connectionToClient, $"<color=#FF3B48>[{currentTime}] 메시지 전송에 실패하였습니다.</color>");
                }
                else
                {
                    /*steamid_Get이 오류인 경우*/
                    TargetReceiveWhisper(connectionToClient, $"<color=#FF3B48>[{currentTime}] <b>{target}</b>을 찾을 수 없습니다.</color>");
                }
            }
            else
            {
                /*GamerServer에도 없는 경우*/
                TargetReceiveWhisper(connectionToClient, $"<color=#FF3B48>[{currentTime}] <b>{target}</b>을 찾을 수 없습니다.</color>");
            }
        }
    }
    /*모든 클라이언트는 메시지를 받아 HandleNewMessage를 진행*/
    [ClientRpc]
    private void RpcHandleMessage(string message)
    {
        OnMessage?.Invoke($"\n{message}");
    }
    /*귓속말 대상 클라이언트도 메시지를 받아 HandleNewMessage를 진행*/
    [TargetRpc]
    public void TargetReceiveWhisper(NetworkConnection target, string message)
    {
        ReceiveWhisper(message);
    }
    private void ReceiveWhisper(string message)
    {
        string coloredMessage = $"{message}";
        OnMessage?.Invoke($"\n{coloredMessage}");
    }
    /*로딩 중 서버에 저장한 닉네임값으로 해당 유저가 서버에 있는지 확인*/
    private NetworkConnectionToClient FindPlayerConnection(string nickname)
    {
        foreach (var conn in NetworkServer.connections.Values)
        {
            if (conn != null)
            {
                ServerDataManger dataManager = conn.identity.GetComponent<ServerDataManger>();
                if (dataManager != null && dataManager.playerName.Equals(nickname, StringComparison.OrdinalIgnoreCase))
                {
                    return conn;
                }
            }
        }
        return null;
    }
    /*서버에서 닉네임을 받아 steamID를 반환*/
    private string FindPlayerSteamID(string nickname)
    {
        ServerDataManger[] allDataManagers = FindObjectsOfType<ServerDataManger>();
        foreach (ServerDataManger dataManager in allDataManagers)
        {
            if (dataManager.playerName.Equals(nickname, StringComparison.OrdinalIgnoreCase))
            {
                return dataManager.SteamID_;
            }
        }
        return null;
    }
    /*커스터마이징에서 의상 변경요청 시 DB에서 해당 의상 변경 적용*/
    [Command]
    private void Change_ChaCloths(NetworkIdentity ClientIdentity, int chaNum, string type, int OutfitID, string UId)
    {
        lobbyserverscript.StartCoroutine(lobbyserverscript.UpdateChaCloth(ClientIdentity, chaNum, type, OutfitID, UId));
    }
    /*의상 변경이 완료되면 클라이언트에 저장 
    변경 완료된 의상 정보를 ClientDataManager에 업데이트 후 해당 작업이 완료됩니다. (lobbyserverscript에서 yield return으로 전송을 기다린 후 해당 전송을 진행)*/
    [TargetRpc]
    public void ChaClothsDataSendToClient(NetworkConnectionToClient target, bool sucfail, int chaNum, string type, int OutfitID)
    {
        if(sucfail == true)
        {
            string outfitIDStr = OutfitID.ToString();
            ClientDataManager.Instance.UpdateCharacterOutfit(chaNum, type, outfitIDStr);
        }
        else
        {
            custhings.Cus_Log_panel_fail.SetActive(true);
            EventSystem.current.SetSelectedGameObject(custhings.Cus_Log_panel_fail_btn.gameObject);
            if(UisoundManager != null)
                UisoundManager.PlayWarringSound();
        }
    }
    /*의상 구매 요청이 온 경우 해당 의상이 존재하는지 확인 후 DB에서 재화가 충분한지 확인, 구매 진행*/
    [Command]
    private void BuyButton_Check(NetworkIdentity ClientIdentity, int ChaNum, string Type, string Description, string ItemName, string Price, string Price_Type, bool isWorn, string UId)
    {
        var matchingOutfits = outfitDataList.outfits.Where(outfit => 
            outfit.outfitName == ItemName && 
            outfit.outfit_Type == Type && 
            outfit.CharaterNum == ChaNum && 
            outfit.image.name == Description).ToList();

        if (matchingOutfits.Count == 1)
        {
            var outfit = matchingOutfits[0];

            lobbyserverscript.StartCoroutine(lobbyserverscript.Buy_Items(ClientIdentity, ChaNum, Type, Description, Price_Type, Price, isWorn, UId));
        }
    }
    /*의상 구매 결과
    구매 의상 정보를 ClientDataManager에 업데이트 후 해당 작업이 완료됩니다. (lobbyserverscript에서 yield return으로 전송을 기다린 후 해당 전송을 진행)*/*/
    [TargetRpc]
    public void ItemDataSendToClient(NetworkConnectionToClient target, bool ItemExist, bool EnoughPoint, bool WornDone, int CP, int BP)
    {
        NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
        /*이미 아이템을 가진 경우*/
        if(ItemExist)
        {
            shopbase.ActivateObject(shopbase.currentIndexList);
            ClientDataManager.Instance.CostUpdate(CP, BP);
            shopbase.ReLoadPoint();
            shopthings.Shop_Log_Fail.SetActive(true);
            EventSystem.current.SetSelectedGameObject(shopthings.Shop_Log_Fail_btn.gameObject);
            if(UisoundManager != null)
                UisoundManager.PlayWarringSound();
        }
        /*재화가 부족한 경우*/
        else if(!ItemExist && !EnoughPoint)
        {
            shopbase.ActivateObject(shopbase.currentIndexList);
            ClientDataManager.Instance.CostUpdate(CP, BP);
            shopbase.ReLoadPoint();
            shopthings.Shop_Log_Fail2.SetActive(true);
            EventSystem.current.SetSelectedGameObject(shopthings.Shop_Log_Fail_btn2.gameObject);
            if(UisoundManager != null)
                UisoundManager.PlayWarringSound();
        }
        /*구매 성공*/
        else if(!ItemExist && EnoughPoint)
        {
            shopbase.ActivateObject(shopbase.currentIndexList);
            ClientDataManager.Instance.CostUpdate(CP, BP);
            shopbase.ReLoadPoint();

            if(WornDone)
            {
                shopbase.ReLoadCha();
            }
            shopthings.Shop_Log_Success.SetActive(true);
            EventSystem.current.SetSelectedGameObject(shopthings.Shop_Log_Success_btn.gameObject);
            if(UisoundManager != null)
                UisoundManager.PlayBuySound();
        }
    }
    /*캐릭터 정보 업데이트*/
    [TargetRpc]
    public void CharacterDataSendToClient(NetworkConnectionToClient target, LodingServerScript.CharactersResponse response)
    {
        ClientDataManager.Instance.UpdateCharacterData(response);
    }
    /*의상 업데이트*/
    [TargetRpc]
    public void OutfitDataSendToClient(NetworkConnectionToClient target, LodingServerScript.OutfitsResponse response)
    {
        ClientDataManager.Instance.UpdateOutfitData(response);
    }
    /*악세사리 업데이트*/
    [TargetRpc]
    public void AccDataSendToClient(NetworkConnectionToClient target, LodingServerScript.AccessoriesResponse response)
    {
        ClientDataManager.Instance.UpdateAccessoriesData(response);
    }
}
