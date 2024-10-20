using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using System;
using System.Collections.Generic;
using UnityEngine.EventSystems;

/*동작을 쉽게 보기위해 포트폴리오에서는 서버동작과 클라이언트동작을 합쳐두었습니다.*/
/*실제로는 [TargetRpc]가 Loading_Client에서 동작합니다.*/
public class RoomData_Server : NetworkBehaviour
{
    /*방 비밀번호 변경을 받으면 해당 방의 비밀번호르 변경함*/
    [Command]
    private void CmdChangeRoomPass(NetworkIdentity ClientIdentity, string roomPass, int roomNumber, Guid RoomId)
    {
        Room roomToChange = RoomManager.Instance.rooms.FirstOrDefault(room => room.RoomNumber == roomNumber);
        if (roomToChange != null && roomToChange.Id == RoomId)
        {
            roomToChange.Password = roomPass;
            Scene targetScene = roomToChange.RoomScene;
            foreach (GameObject obj in targetScene.GetRootGameObjects())
            {
                RoomData roomData = obj.GetComponentInChildren<RoomData>(true);
                if (roomData != null)
                {
                    roomData.CurrnetPassword = roomToChange.Password;
                }
            }
            
        }
    }
    /*방 제목 변경을 받으면 방 제목을 변경함*/
    [Command]
    private void CmdChangeRoomTitle(NetworkIdentity ClientIdentity, string roomTitle, int roomNumber, Guid RoomId)
    {
        Room roomToChange = RoomManager.Instance.rooms.FirstOrDefault(room => room.RoomNumber == roomNumber);
        if (roomToChange != null && roomToChange.Id == RoomId)
        {
            roomToChange.RoomName = roomTitle;
            Scene targetScene = roomToChange.RoomScene;
            foreach (GameObject obj in targetScene.GetRootGameObjects())
            {
                RoomData roomData = obj.GetComponentInChildren<RoomData>(true);
                if (roomData != null)
                {
                    roomData.CurrnetRoomName = roomToChange.RoomName;
                }
            }
        }
    }
    /*방을 나갈 경우 방장인 경우와 구분하여 조정*/
    [Command]
    private void CmdPlayerWantsToLeaveRoom(NetworkIdentity ClientIdentity, int roomNumber)
    {
        Room roomToChange = RoomManager.Instance.rooms.FirstOrDefault(room => room.RoomNumber == roomNumber);
        if (roomToChange != null && roomToChange.Id == RoomId)
        {
            Scene targetScene = roomToChange.RoomScene;
            Scene LobbyScene = SceneManager.GetSceneByName("M_Loading");
            /*유저가 한명이었으면 방 삭제 진행*/
            if(roomToChange.CurrentRoomPNumber == 1)
            {
                RoomManager.Instance.rooms.RemoveAll(room => room.RoomNumber == roomNumber && room.Id == RoomId);
                RoomManager.Instance.availableRoomNumbers.Enqueue(roomNumber);
                
                foreach(GameObject obj in targetScene.GetRootGameObjects())
                {
                    RoomData roomData = obj.GetComponentInChildren<RoomData>(true);
                    if (roomData != null && roomData != this)
                    {
                        SceneManager.MoveGameObjectToScene(roomData.PlayerObj, LobbyScene);
                        NetworkServer.UnSpawn(obj);
                        NetworkServer.Destroy(obj);
                    }
                    RoomCheck roomCheck = obj.GetComponentInChildren<RoomCheck>();
                    if (roomCheck != null)
                    {
                        Destroy(roomCheck.gameObject);
                    }
                }
                /*해당 방의 유저를 Lobby로 이동*/
                RpcAllPChangeScene("Ingame12");
                /*서버 측에서 방으로 사용되던 씬을 비동기로 닫기*/
                if (targetScene.isLoaded)
                {
                    StartCoroutine(UnloadScene(targetScene));
                }
            }
            /*방에 다른 유저가 있는 경우*/
            else
            {
                int NextHost = -1;
                /*방에서 나가는 위치의 정보를 초기화*/
                foreach (GameObject obj in targetScene.GetRootGameObjects())
                {
                    RoomData roomData = obj.GetComponentInChildren<RoomData>(true);
                    if (roomData != null && roomData != this)
                    {
                        roomData.inventory[this.PlayerIndex] = new Item
                        {
                            PlayerExist = false,
                            FakeHost = false,
                            PlayerNickname = null,
                            PlayerLevel = 0,
                            PlayerTexture = null,
                            PlayerMainCharacterID = null,
                            PlayerTopOutfitID = null,
                            PlayerBottomOutfitID = null,
                            PlayerShoesOutfitID = null
                        };
                        roomData.CurrnetCurrentRoomPNumber -= 1;
                        /*존재하는 유저 중 가장 앞선 index를 다음 방장으로 선정*/
                        if(NextHost == -1)
                            NextHost = roomData.PlayerIndex;
                    }
                }
                /*만약 나간 유저가 방장이었다면 NextHost 유저의 FakeHost를 true로 지정*/
                if(inventory[this.PlayerIndex].FakeHost == true)
                {
                    foreach (GameObject obj in targetScene.GetRootGameObjects())
                    {
                        RoomData roomData = obj.GetComponentInChildren<RoomData>(true);
                        if (roomData != null && roomData != this)
                        {
                            Item OteroneItem = roomData.inventory[NextHost];
                            roomData.inventory[NextHost] = new Item
                            {
                                PlayerExist = OteroneItem.PlayerExist,
                                FakeHost = true,
                                PlayerNickname = OteroneItem.PlayerNickname,
                                PlayerLevel = OteroneItem.PlayerLevel,
                                PlayerTexture = OteroneItem.PlayerTexture,
                                PlayerMainCharacterID = OteroneItem.PlayerMainCharacterID,
                                PlayerTopOutfitID = OteroneItem.PlayerTopOutfitID,
                                PlayerBottomOutfitID = OteroneItem.PlayerBottomOutfitID,
                                PlayerShoesOutfitID = OteroneItem.PlayerShoesOutfitID
                            };
                        }
                    }
                }
                /*유저가 나갔으므로 현재 방의 유저수를 빼주고 해당 인덱스로 입장 가능하도록 slot false로 지정*/
                roomToChange.CurrentRoomPNumber -= 1;
                roomToChange.PlayerSlots[this.PlayerIndex] = false;
            }
            SceneManager.MoveGameObjectToScene(PlayerObj, LobbyScene);
            TargetRooomChangeScene(ClientIdentity.connectionToClient, "Ingame12");
            NetworkServer.UnSpawn(this.gameObject);
            NetworkServer.Destroy(this.gameObject);
        }
    }
    /*서버에서 클라이언트의 연결이 끊어졌을 때(강제 종료)에도 방을 나간 동작을 수행하기 위해 사용*/
    public void HandleDisconnection()
    {
        Room roomToChange = RoomManager.Instance.rooms.FirstOrDefault(room => room.RoomNumber == CurrnetRoomNumber);
        if (roomToChange != null && roomToChange.Id == RoomId)
        {
            Scene targetScene = roomToChange.RoomScene;
            Scene LobbyScene = SceneManager.GetSceneByName("M_Loading");
            if(roomToChange.CurrentRoomPNumber == 1)
            {
                RoomManager.Instance.rooms.RemoveAll(room => room.RoomNumber == CurrnetRoomNumber && room.Id == RoomId);
                RoomManager.Instance.availableRoomNumbers.Enqueue(CurrnetRoomNumber);
                
                foreach(GameObject obj in targetScene.GetRootGameObjects())
                {
                    RoomData roomData = obj.GetComponentInChildren<RoomData>(true);
                    if (roomData != null && roomData != this)
                    {
                        SceneManager.MoveGameObjectToScene(roomData.PlayerObj, LobbyScene);
                        NetworkServer.UnSpawn(obj);
                        NetworkServer.Destroy(obj);
                    }
                    RoomCheck roomCheck = obj.GetComponentInChildren<RoomCheck>();
                    if (roomCheck != null)
                    {
                        Destroy(roomCheck.gameObject);
                    }
                }
                RpcAllPChangeScene("Ingame12");
                
                if (targetScene.isLoaded)
                {
                    StartCoroutine(UnloadScene(targetScene));
                }
            }
            else
            {
                int NextHost = -1;
                foreach (GameObject obj in targetScene.GetRootGameObjects())
                {
                    RoomData roomData = obj.GetComponentInChildren<RoomData>(true);
                    if (roomData != null && roomData != this)
                    {
                        roomData.inventory[this.PlayerIndex] = new Item
                        {
                            PlayerExist = false,
                            FakeHost = false,
                            PlayerNickname = null,
                            PlayerLevel = 0,
                            PlayerTexture = null,
                            PlayerMainCharacterID = null,
                            PlayerTopOutfitID = null,
                            PlayerBottomOutfitID = null,
                            PlayerShoesOutfitID = null
                        };
                        roomData.CurrnetCurrentRoomPNumber -= 1;
                        if(NextHost == -1)
                            NextHost = roomData.PlayerIndex;
                    }
                }
                if(inventory[this.PlayerIndex].FakeHost == true)
                {
                    foreach (GameObject obj in targetScene.GetRootGameObjects())
                    {
                        RoomData roomData = obj.GetComponentInChildren<RoomData>(true);
                        if (roomData != null && roomData != this)
                        {
                            Item OteroneItem = roomData.inventory[NextHost];
                            roomData.inventory[NextHost] = new Item
                            {
                                PlayerExist = OteroneItem.PlayerExist,
                                FakeHost = true,
                                PlayerNickname = OteroneItem.PlayerNickname,
                                PlayerLevel = OteroneItem.PlayerLevel,
                                PlayerTexture = OteroneItem.PlayerTexture,
                                PlayerMainCharacterID = OteroneItem.PlayerMainCharacterID,
                                PlayerTopOutfitID = OteroneItem.PlayerTopOutfitID,
                                PlayerBottomOutfitID = OteroneItem.PlayerBottomOutfitID,
                                PlayerShoesOutfitID = OteroneItem.PlayerShoesOutfitID
                            };
                        }
                    }
                }

                roomToChange.CurrentRoomPNumber -= 1;
                roomToChange.PlayerSlots[this.PlayerIndex] = false;
            }
            SceneManager.MoveGameObjectToScene(PlayerObj, LobbyScene);
            NetworkServer.UnSpawn(this.gameObject);
            NetworkServer.Destroy(this.gameObject);
        }
    }
    /*해당 유저를 특정 씬으로 이동*/
    [TargetRpc]
    private void TargetRooomChangeScene(NetworkConnection target, string sceneName)
    {
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }
    /*방에 있는 모든 유저를 특정 씬으로 이동*/
    [ClientRpc(includeOwner = false)]
    private void RpcAllPChangeScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }
    /*비동기로 씬 닫기*/
    IEnumerator UnloadScene(Scene targetScene)
    {
        AsyncOperation asyncUnload = SceneManager.UnloadSceneAsync(targetScene);

        while (!asyncUnload.isDone)
        {
            yield return null;
        }
    }
    /*유저가 준비버튼 클릭 시*/
    [Command]
    private void CmdChangeRoomReday(NetworkIdentity ClientIdentity, int roomNumber)
    {
        Room roomToChange = RoomManager.Instance.rooms.FirstOrDefault(room => room.RoomNumber == roomNumber);
        if (roomToChange != null && roomToChange.Id == RoomId)
        {
            Scene targetScene = roomToChange.RoomScene;
            Item MyItem = inventory[PlayerIndex];
            foreach (GameObject obj in targetScene.GetRootGameObjects())
            {
                RoomData roomData = obj.GetComponentInChildren<RoomData>(true);
                if (roomData != null)
                {
                    roomData.inventory[this.PlayerIndex] = new Item
                    {
                        PlayerExist = MyItem.PlayerExist,
                        FakeHost = MyItem.FakeHost,
                        PlayerNickname = MyItem.PlayerNickname,
                        PlayerLevel = MyItem.PlayerLevel,
                        PlayerTexture = MyItem.PlayerTexture,
                        PlayerMainCharacterID = MyItem.PlayerMainCharacterID,
                        PlayerTopOutfitID = MyItem.PlayerTopOutfitID,
                        PlayerBottomOutfitID = MyItem.PlayerBottomOutfitID,
                        PlayerShoesOutfitID = MyItem.PlayerShoesOutfitID,
                        Ready = !MyItem.Ready
                    };
                }
            }
        }
    }
    /*방장이 게임시작 버튼 클릭 시*/
    [Command]
    private void CmdHostStart(NetworkIdentity ClientIdentity, int roomNumber)
    {
        Room roomToChange = RoomManager.Instance.rooms.FirstOrDefault(room => room.RoomNumber == roomNumber);
        if (roomToChange != null && roomToChange.Id == RoomId)
        {
            /*방의 게임시작을 true로 변경해 방으로의 입장을 막고 방 목록에서 게임 진행 중을 표기*/
            roomToChange.RoomStart = true;
            Scene targetScene = roomToChange.RoomScene;
            foreach (GameObject obj in targetScene.GetRootGameObjects())
            {
                RoomData roomData = obj.GetComponentInChildren<RoomData>(true);
                if (roomData != null)
                {
                    NetworkIdentity targetIdentity = roomData.gameObject.GetComponent<NetworkIdentity>();
                    if(targetIdentity != null && targetIdentity.connectionToClient != null)
                    {
                        /*유저들의 버튼들과 메뉴바 활용을 막고 로딩 씬 시작*/
                        roomData.Lock_Count(targetIdentity.connectionToClient);
                    }
                    /*게임 종료 후 방으로 돌아왔을 경우 모든 유저가 준비완료 상태인 것을 막기 위해
                    준비완료를 false로 변경*/
                    for(int i = 0; i < 4; i++)
                    {
                        Item Qitem = roomData.inventory[i];
                        roomData.inventory[i] = new Item
                        {
                            PlayerExist = Qitem.PlayerExist,
                            FakeHost = Qitem.FakeHost,
                            PlayerNickname = Qitem.PlayerNickname,
                            PlayerLevel = Qitem.PlayerLevel,
                            PlayerTexture = Qitem.PlayerTexture,
                            PlayerMainCharacterID = Qitem.PlayerMainCharacterID,
                            PlayerTopOutfitID = Qitem.PlayerTopOutfitID,
                            PlayerBottomOutfitID = Qitem.PlayerBottomOutfitID,
                            PlayerShoesOutfitID = Qitem.PlayerShoesOutfitID,
                            Ready = false
                        };
                    }
                }
            }
        }
        /*방의 유저들의 상황을 저장할 오브젝트 생성*/
        GameObject RoomCheck = Instantiate(myNetworkGameRoomPrefab);
        NetworkServer.Spawn(RoomCheck);
        Scene thisScene = gameObject.scene;
        SceneManager.MoveGameObjectToScene(RoomCheck, thisScene);
    }
    /*버튼과 메뉴바의 활용을 멈추고 코루틴으로 5초 뒤 다시 활성화하게 진행(게임에 입장하지 못할 경우 모든 유저가 방으로 다시 돌아오기 때문)*/
    [TargetRpc]
    private void Lock_Count(NetworkConnection target)
    {
        roomthings.RoomExitBtn_T.interactable = false;
        roomthings.PReadybtn.interactable = false;
        canOpenContextMenu = false;
        StartCoroutine(EnableButtonsAfterDelay_Lock(5f));
        StartCoroutine(Count_DownText());
        if(UisoundManager != null)
            UisoundManager.HReadySound();
    }
    /*클라이언트가 현재 방의 정보 저장이 완료되었을 경우 GetRoomData = true
     모든 유저들의 GetGameScene, GetCharacters, GetRoomData가 true 일 경우 게임서버로 이동 함*/
    [Command]
    private void CmdCGetRoomData(NetworkIdentity ClientIdentity)
    {
        this.GetRoomData = true;
        TargetRequestCharacterData_1(ClientIdentity.connectionToClient);
    }
    /*게임에서는 메인캐릭터를 포함한 모든 캐릭터의 의상데이터가 필요하므로 이것도 더미데이터에 저장*/
    [TargetRpc]
    private void TargetRequestCharacterData_1(NetworkConnection target)
    {
        List<CharacterCostumeInfo> allCharacterCostumes = new List<CharacterCostumeInfo>();
        foreach (var character in ClientDataManager.Instance.CharacterData.characters)
        {
            int.TryParse(character.TopOutfitID, out int topOutfitID);
            int.TryParse(character.BottomOutfitID, out int bottomOutfitID);
            int.TryParse(character.ShoesOutfitID, out int shoesOutfitID);

            CharacterCostumeInfo costumeInfo = new CharacterCostumeInfo()
            {
                CharacterID = character.CharacterType,
                TopDescription = GetOutfitDescription(topOutfitID),
                BottomDescription = GetOutfitDescription(bottomOutfitID),
                ShoesDescription = GetOutfitDescription(shoesOutfitID)
            };
            allCharacterCostumes.Add(costumeInfo);
        }
        NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
        if (roomthings.sliderCoroutine != null)
        {
            StopCoroutine(roomthings.sliderCoroutine);
        }
        roomthings.sliderCoroutine = StartCoroutine(IncreaseSliderOverTime(0.3f, 0.5f));
        CDatas.characterDataList = allCharacterCostumes;
        if (roomthings.sliderCoroutine != null)
        {
            StopCoroutine(roomthings.sliderCoroutine);
        }
        roomthings.sliderCoroutine = StartCoroutine(IncreaseSliderOverTime(0.4f, 0.5f));
        CmdCGetCharacters(opponentIdentity);
    }
    /*클라이언트 의상정보 저장이 완료되었을 경우 GetCharacters = true
     모든 유저들의 GetGameScene, GetCharacters, GetRoomData가 true 일 경우 게임서버로 이동 함*/
    [Command]
    private void CmdCGetCharacters(NetworkIdentity ClientIdentity)
    {
        this.GetCharacters = true;
        RpcLoadGameScene(ClientIdentity.connectionToClient);
    }
    /*씬을 비동기로 로드*/
    [TargetRpc]
    private void RpcLoadGameScene(NetworkConnection target)
    {
        if (roomthings.sliderCoroutine != null)
        {
            StopCoroutine(roomthings.sliderCoroutine);
        }
        roomthings.sliderCoroutine = StartCoroutine(IncreaseSliderOverTime(0.6f, 0.5f));
        StartCoroutine(LoadSceneCoroutine("gamescence1 2"));
    }
    private IEnumerator LoadSceneCoroutine(string sceneName)
    {
        bool sceneAlreadyLoaded = false;
        NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
        /*방에서 게임으로 이동을 못했을 경우 비동기로 로드된 씬이 남아있기 때문에
        동일한 씬을 여러개 로드하는 것을 방지하기 위함*/
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.name == sceneName)
            {  
                sceneAlreadyLoaded = true;
                CmdSetGameSceneLoaded(opponentIdentity);
                break;
            }
        }

        if (!sceneAlreadyLoaded)
        {
            /*해당 스크립트는 게임서버로 이동하면서 클라이언트 측에서 삭제될 것이므로 비동기로 로드한 씬을 활성화하기 위한 
            allowSceneActivation를 더미데이터에 저장*/
            CDatas.GamesceneAsync = SceneManager.LoadSceneAsync(sceneName);
            CDatas.GamesceneAsync.allowSceneActivation = false;
            while (!CDatas.GamesceneAsync.isDone)
            {
                if (CDatas.GamesceneAsync.progress >= 0.9f)
                {
                    CmdSetGameSceneLoaded(opponentIdentity);
                    break;
                }
                yield return null;
            }
        }
    }
    /*클라이언트 의상정보 저장이 완료되었을 경우 GetRoomData = true
     모든 유저들의 GetGameScene, GetCharacters, GetRoomData가 true 일 경우 게임서버로 이동 함*/
    [Command]
    private void CmdSetGameSceneLoaded(NetworkIdentity clientIdentity)
    {
        this.GetGameScene = true;
    }
    /*GameCheck 스크립트를 통해 70초 동안 모든 유저의 GetGameScene, GetCharacters, GetRoomData가 true인지를 확인하고 모두 true일 경우 클라이언트가 진행*/
    [TargetRpc]
    public void GameCheckDone(NetworkConnection target, string UId)
    {
        if (roomthings.sliderCoroutine != null)
        {
            StopCoroutine(roomthings.sliderCoroutine);
        }
        roomthings.sliderCoroutine = StartCoroutine(IncreaseSliderOverTime(0.7f, 0.5f));
        /*clientGameManger를 통해 메시지를 보내고 해당 서버로 이동함*/
        var clientGameManger = FindObjectOfType<Insight.ClientGameManager__1>();
        if (clientGameManger != null)
        {
            clientGameManger.SendJoinGameMsg(UId);
        }
    }
    /*게임으로 이동을 실패햇을 경우 로딩 관련 값을 초기화*/
    [TargetRpc]
    public void GameCheckFail(NetworkConnection target)
    {
        NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
        CmdResa(opponentIdentity);
        //Destroy(CDatas.gameObject);
        CDatas = null;

        Image panelImage = roomthings.GLoadPanel.GetComponent<Image>();
        Color panelColor = panelImage.color;
        panelColor.a = 0;
        panelImage.color = panelColor;

        Image sliderBackgroundImage = roomthings.GLoadSlider.transform.Find("Background").GetComponent<Image>();
        Color sliderBgColor = sliderBackgroundImage.color;
        sliderBgColor.a = 0;
        sliderBackgroundImage.color = sliderBgColor;

        Color textVertexColor = roomthings.GLoadingSlider_Text.color;
        textVertexColor.a = 0;
        roomthings.GLoadingSlider_Text.color = textVertexColor;
        roomthings.GLoadSlider.value = 0;
        roomthings.GLoadPanel.SetActive(false);
    }
    /*서버 측에서도 로딩 관련 값 변경*/
    [Command]
    private void CmdResa(NetworkIdentity clientIdentity)
    {
        this.GetGameScene = false;
        this.GetCharacters = false;
        this.GetRoomData = false;
        this.NetUniqueID = null;
    }
    /*게임 종료 후 방으로 왔을 때를 위해*/
    public void ReConnectRoom()
    {
        this.ChangeProgress = false;
        this.GetGameScene = false;
        this.GetCharacters = false;
        this.GetRoomData = false;
        this.NetUniqueID = null;

        Room roomToChange = RoomManager.Instance.rooms.FirstOrDefault(room => room.RoomNumber == this.CurrnetRoomNumber);
        if (roomToChange != null && roomToChange.Id == this.RoomId && roomToChange.RoomStart)
        {
            roomToChange.RoomStart = false;
        }
        TargetRooomChangeScene(connectionToClient, "Ingame");
                                            
    }
    /*게임서버에서 먼저 게임이 끝나서 로비로 가는 유저를 위해 서버 측에서 사용*/
    public void HandleDisconnectionToLobby()
    {
        Room roomToChange = RoomManager.Instance.rooms.FirstOrDefault(room => room.RoomNumber == CurrnetRoomNumber);
        if (roomToChange != null && roomToChange.Id == RoomId)
        {
            Scene targetScene = roomToChange.RoomScene;
            Scene LobbyScene = SceneManager.GetSceneByName("M_Loading");
            if(roomToChange.CurrentRoomPNumber == 1)
            {
                RoomManager.Instance.rooms.RemoveAll(room => room.RoomNumber == CurrnetRoomNumber && room.Id == RoomId);
                RoomManager.Instance.availableRoomNumbers.Enqueue(CurrnetRoomNumber);
                
                foreach(GameObject obj in targetScene.GetRootGameObjects())
                {
                    RoomData roomData = obj.GetComponentInChildren<RoomData>(true);
                    if (roomData != null && roomData != this)
                    {
                        SceneManager.MoveGameObjectToScene(roomData.PlayerObj, LobbyScene);
                        NetworkServer.UnSpawn(obj);
                        NetworkServer.Destroy(obj);
                    }
                    RoomCheck roomCheck = obj.GetComponentInChildren<RoomCheck>();
                    if (roomCheck != null)
                    {
                        Destroy(roomCheck.gameObject);
                    }
                }
                RpcAllPChangeScene("Ingame12");
                
                if (targetScene.isLoaded)
                {
                    StartCoroutine(UnloadScene(targetScene));
                }
            }
            else
            {
                int NextHost = -1;
                foreach (GameObject obj in targetScene.GetRootGameObjects())
                {
                    RoomData roomData = obj.GetComponentInChildren<RoomData>(true);
                    if (roomData != null && roomData != this)
                    {
                        roomData.inventory[this.PlayerIndex] = new Item
                        {
                            PlayerExist = false,
                            FakeHost = false,
                            PlayerNickname = null,
                            PlayerLevel = 0,
                            PlayerTexture = null,
                            PlayerMainCharacterID = null,
                            PlayerTopOutfitID = null,
                            PlayerBottomOutfitID = null,
                            PlayerShoesOutfitID = null
                        };
                        roomData.CurrnetCurrentRoomPNumber -= 1;
                        if(NextHost == -1)
                            NextHost = roomData.PlayerIndex;
                    }
                }
                if(inventory[this.PlayerIndex].FakeHost == true)
                {
                    foreach (GameObject obj in targetScene.GetRootGameObjects())
                    {
                        RoomData roomData = obj.GetComponentInChildren<RoomData>(true);
                        if (roomData != null && roomData != this)
                        {
                            Item OteroneItem = roomData.inventory[NextHost];
                            roomData.inventory[NextHost] = new Item
                            {
                                PlayerExist = OteroneItem.PlayerExist,
                                FakeHost = true,
                                PlayerNickname = OteroneItem.PlayerNickname,
                                PlayerLevel = OteroneItem.PlayerLevel,
                                PlayerTexture = OteroneItem.PlayerTexture,
                                PlayerMainCharacterID = OteroneItem.PlayerMainCharacterID,
                                PlayerTopOutfitID = OteroneItem.PlayerTopOutfitID,
                                PlayerBottomOutfitID = OteroneItem.PlayerBottomOutfitID,
                                PlayerShoesOutfitID = OteroneItem.PlayerShoesOutfitID
                            };
                        }
                    }
                }

                roomToChange.CurrentRoomPNumber -= 1;
                roomToChange.PlayerSlots[this.PlayerIndex] = false;
            }
            SceneManager.MoveGameObjectToScene(PlayerObj, LobbyScene);
            TargetRooomChangeScene(connectionToClient, "Ingame12");
            NetworkServer.UnSpawn(this.gameObject);
            NetworkServer.Destroy(this.gameObject);
        }                     
    }
    /*유저가 방에 입장 시 입장한 유저의 값을 서버에 저장*/
    [Command]
    void CmdSetRoomPlayer(NetworkIdentity ClientIdentity, int index, bool fakeHost,
    string nickname, int level, byte[] textureBytes, string mainCharId, 
    string topOutfitDesc, string bottomOutfitDesc, string shoesOutfitDesc)
    {
        inventory[index] = new Item
        {
            PlayerExist = true,
            FakeHost = fakeHost,
            PlayerNickname = nickname,
            PlayerLevel = level,
            PlayerTexture = textureBytes,
            PlayerMainCharacterID = mainCharId,
            PlayerTopOutfitID = topOutfitDesc,
            PlayerBottomOutfitID = bottomOutfitDesc,
            PlayerShoesOutfitID = shoesOutfitDesc
        };
    }
    /*입장한 유저의 값은 다른 유저에게 보내고, 다른 유저의 값은 입장한 유저가 받음*/
    [Command]
    private void OriginalPlayer_R1(NetworkIdentity ClientIdentity, int roomNumber)
    {
        Room roomToChange = RoomManager.Instance.rooms.FirstOrDefault(room => room.RoomNumber == roomNumber);
        if (roomToChange != null && roomToChange.Id == RoomId)
        {
            Scene targetScene = roomToChange.RoomScene;
            foreach (GameObject obj in targetScene.GetRootGameObjects())
            {
                RoomData roomData = obj.GetComponentInChildren<RoomData>(true);
                if (roomData != null && roomData != this)
                {
                    roomData.CurrnetCurrentRoomPNumber = roomToChange.CurrentRoomPNumber;

                    Item MyItem = this.inventory[PlayerIndex];
                    roomData.inventory[this.PlayerIndex] = new Item
                    {
                        PlayerExist = MyItem.PlayerExist,
                        FakeHost = MyItem.FakeHost,
                        PlayerNickname = MyItem.PlayerNickname,
                        PlayerLevel = MyItem.PlayerLevel,
                        PlayerTexture = MyItem.PlayerTexture,
                        PlayerMainCharacterID = MyItem.PlayerMainCharacterID,
                        PlayerTopOutfitID = MyItem.PlayerTopOutfitID,
                        PlayerBottomOutfitID = MyItem.PlayerBottomOutfitID,
                        PlayerShoesOutfitID = MyItem.PlayerShoesOutfitID
                    };

                    Item OtherItem = roomData.inventory[roomData.PlayerIndex];
                    this.inventory[roomData.PlayerIndex] = new Item
                    {
                        PlayerExist = OtherItem.PlayerExist,
                        FakeHost = OtherItem.FakeHost,
                        PlayerNickname = OtherItem.PlayerNickname,
                        PlayerLevel = OtherItem.PlayerLevel,
                        PlayerTexture = OtherItem.PlayerTexture,
                        PlayerMainCharacterID = OtherItem.PlayerMainCharacterID,
                        PlayerTopOutfitID = OtherItem.PlayerTopOutfitID,
                        PlayerBottomOutfitID = OtherItem.PlayerBottomOutfitID,
                        PlayerShoesOutfitID = OtherItem.PlayerShoesOutfitID,
                        Ready = OtherItem.Ready
                    };
                }
            }
        }
    }
    /*방에서 위치 변경 시 입장을 막기 위해 우선 방 정보에 해당 slot을 true로 해서 입장을 막고
     기존 위치는 초기화, 변경 위치에 기존 값을 넣고 이 과정이 완료되면 해당 slot에 입장이 가능하도록 false로 지정*/
    [Command]
    private void CmdMaunOption1(NetworkIdentity ClientIdentity, int roomNumber, int index)
    {
        Room roomToChange = RoomManager.Instance.rooms.FirstOrDefault(room => room.RoomNumber == roomNumber);
        if (roomToChange != null && roomToChange.Id == RoomId && roomToChange.PlayerSlots[index] == false)
        {
            Scene targetScene = roomToChange.RoomScene;
            roomToChange.PlayerSlots[index] = true;
            Item MyItem = this.inventory[this.PlayerIndex];
            foreach (GameObject obj in targetScene.GetRootGameObjects())
            {
                RoomData roomData = obj.GetComponentInChildren<RoomData>(true);
                if (roomData != null)
                {
                    roomData.inventory[this.PlayerIndex] = new Item
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
                        Ready = false
                    };

                    roomData.inventory[index] = new Item
                    {
                        PlayerExist = MyItem.PlayerExist,
                        FakeHost = MyItem.FakeHost,
                        PlayerNickname = MyItem.PlayerNickname,
                        PlayerLevel = MyItem.PlayerLevel,
                        PlayerTexture = MyItem.PlayerTexture,
                        PlayerMainCharacterID = MyItem.PlayerMainCharacterID,
                        PlayerTopOutfitID = MyItem.PlayerTopOutfitID,
                        PlayerBottomOutfitID = MyItem.PlayerBottomOutfitID,
                        PlayerShoesOutfitID = MyItem.PlayerShoesOutfitID,
                        Ready = false
                    };
                }
            }
            roomToChange.PlayerSlots[this.PlayerIndex] = false;
            this.PlayerIndex = index;
        }
    }
    /*방장 위임 시 방장 값 변경 (+ 테스트 중 방장 변경 후 준비를 다시하는 것이 맞다고 판단하여 ready= false)*/
    [Command]
    private void CmdMaunOption2(NetworkIdentity ClientIdentity, int roomNumber, int index)
    {
        Room roomToChange = RoomManager.Instance.rooms.FirstOrDefault(room => room.RoomNumber == roomNumber);
        if (roomToChange != null && roomToChange.Id == RoomId && inventory[this.PlayerIndex].FakeHost == true && inventory[index].PlayerExist == true && inventory[index].FakeHost == false)
        {
            Scene targetScene = roomToChange.RoomScene;
            Item MyItem = inventory[this.PlayerIndex];
            Item OtherItem = inventory[index];
            foreach (GameObject obj in targetScene.GetRootGameObjects())
            {
                RoomData roomData = obj.GetComponentInChildren<RoomData>(true);
                if (roomData != null)
                {
                    roomData.inventory[this.PlayerIndex] = new Item
                    {
                        PlayerExist = MyItem.PlayerExist,
                        FakeHost = false,
                        PlayerNickname = MyItem.PlayerNickname,
                        PlayerLevel = MyItem.PlayerLevel,
                        PlayerTexture = MyItem.PlayerTexture,
                        PlayerMainCharacterID = MyItem.PlayerMainCharacterID,
                        PlayerTopOutfitID = MyItem.PlayerTopOutfitID,
                        PlayerBottomOutfitID = MyItem.PlayerBottomOutfitID,
                        PlayerShoesOutfitID = MyItem.PlayerShoesOutfitID,
                        Ready = false
                    };
                    roomData.inventory[index] = new Item
                    {
                        PlayerExist = OtherItem.PlayerExist,
                        FakeHost = true,
                        PlayerNickname = OtherItem.PlayerNickname,
                        PlayerLevel = OtherItem.PlayerLevel,
                        PlayerTexture = OtherItem.PlayerTexture,
                        PlayerMainCharacterID = OtherItem.PlayerMainCharacterID,
                        PlayerTopOutfitID = OtherItem.PlayerTopOutfitID,
                        PlayerBottomOutfitID = OtherItem.PlayerBottomOutfitID,
                        PlayerShoesOutfitID = OtherItem.PlayerShoesOutfitID,
                        Ready = false
                    };
                }
            }
        }
    }
    /*강제퇴장 시*/
    [Command]
    private void CmdMaunOption3(NetworkIdentity ClientIdentity, int roomNumber, int index)
    {
        Room roomToChange = RoomManager.Instance.rooms.FirstOrDefault(room => room.RoomNumber == roomNumber);
        if (roomToChange != null && roomToChange.Id == RoomId && inventory[this.PlayerIndex].FakeHost == true && inventory[index].PlayerExist == true && inventory[index].FakeHost == false)
        {
            Scene targetScene = roomToChange.RoomScene;
            /*서버 측에서 Additve sence 중 로비 scene*/
            Scene LobbyScene = SceneManager.GetSceneByName("M_Loading");
            foreach (GameObject obj in targetScene.GetRootGameObjects())
            {
                RoomData roomData = obj.GetComponentInChildren<RoomData>(true);
                if (roomData != null)
                {
                    roomData.inventory[index] = new Item
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
                        Ready = false
                    };
                    roomData.CurrnetCurrentRoomPNumber -= 1;
                    if(roomData.PlayerIndex == index)
                    {
                        NetworkIdentity targetIdentity = roomData.PlayerObj.GetComponent<NetworkIdentity>();
                        SceneManager.MoveGameObjectToScene(roomData.PlayerObj, LobbyScene);
                        if(targetIdentity != null && targetIdentity.connectionToClient != null)
                        {
                            /*강제퇴장 당하는 유저를 로비 씬으로 이동*/
                            TargetRooomChangeScene(targetIdentity.connectionToClient, "Ingame12");
                            Lobby ExitFor = roomData.PlayerObj.GetComponent<Lobby>();
                            /*강제퇴장 팝업을 띄우기*/
                            ExitFor.TargetActivateRoomExitForced(targetIdentity.connectionToClient);
                        }
                        NetworkServer.UnSpawn(obj);
                        NetworkServer.Destroy(obj);
                    }
                }
            }
            roomToChange.CurrentRoomPNumber -= 1;
            roomToChange.PlayerSlots[index] = false;
        }
    }
}
