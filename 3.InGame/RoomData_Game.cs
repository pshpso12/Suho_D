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


/*해당 스크립트는 클라이언트는 아직 방 씬에 있지만 서버는 게임서버로 변경한 경우 시작*/
public class RoomData_Game : NetworkBehaviour
{
    [SyncVar]
    public int PlayerIndex;
    [SyncVar]
    public int CurrnetRoomNumber;
    [SyncVar]
    public string CurrnetRoomName;
    [SyncVar]
    public int CurrnetMaxRoomPNumber;
    [SyncVar]
    public int CurrnetCurrentRoomPNumber;
    [SyncVar]
    public string CurrnetPassword;
    [SyncVar]
    public Guid RoomId;

    public readonly SyncList<Item> inventory = new SyncList<Item>();

    private RoomBase roombase;
    private Room_Things roomthings;
    
    [SerializeField] private GameObject myNetworkGameRoomPrefab;
    [SerializeField] private GameObject CloneT;

    
    [SyncVar]
    public bool isDummy;
    [SyncVar]
    public bool GetRoomData;
    [SyncVar]
    public bool GetCharacters;
    [SyncVar]
    public bool GetGameScene;
    [SyncVar]
    public bool RealSceneChnage;
    [SyncVar]
    public bool ChangeProgress;
    
    public string NetUniqueID;
    public Clone_Datas CDatas;
    public int RecivePnum;
    
    public struct CloneDataMessage : NetworkMessage
    {
        public int PlayerIndex;
        public int CurrentRoomNumber;
        public Guid RoomId;
        public int ImageIndex;
        public List<GameItem> Gameinventory;
        public List<CharacterCostumeInfo> CharacterDataList;
    }
    public GameData GameData_Own;
    
    void Start()
    {
        if(isClient)
        {
            DontDestroyOnLoad(this);
        }
        if(isClient && isOwned)
        {
            GameObject SceneManGameObject = GameObject.Find("SceneManger");
            if(SceneManGameObject != null)
                roombase = SceneManGameObject.GetComponent<RoomBase>();
            if (roomthings.sliderCoroutine != null)
            {
                StopCoroutine(roomthings.sliderCoroutine);
            }
            roomthings.sliderCoroutine = StartCoroutine(IncreaseSliderOverTime(0.7f, 0.5f));
            CDatas = FindObjectOfType<Clone_Datas>();
            NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
            CheckPlayerNumber(opponentIdentity);
        }
    }
    [Command]
    void CheckPlayerNumber(NetworkIdentity ClientIdentity)
    {
        StartCoroutine(RecievePlayerNumer(ClientIdentity));
    }
    /*RoomCheck 스크립트에서 보낸 인원 값 확인*/
    IEnumerator RecievePlayerNumer(NetworkIdentity ClientIdentity)
    {
        var GameRegi = FindObjectOfType<Insight.GameRegistration_Kihana>();
        if (GameRegi != null)
        {
            while(GameRegi.RecievePNum == 200)
            {
                yield return new WaitForSeconds(0.5f);
            }
            RecivePnum = GameRegi.RecievePNum;
            RpcRecievePlayerNumber(ClientIdentity.connectionToClient, GameRegi.RecievePNum);
        }    
    }
    [TargetRpc]
    void RpcRecievePlayerNumber(NetworkConnection target, int PNum)
    {
        RecivePnum = PNum;
        if (roomthings.sliderCoroutine != null)
        {
            StopCoroutine(roomthings.sliderCoroutine);
        }
        roomthings.sliderCoroutine = StartCoroutine(IncreaseSliderOverTime(0.75f, 0.5f));
        SendCloneT();
    }
    /*클라이언트에 만들어 두었던 정보 데이터를 게임서버로 전송*/
    void SendCloneT()
    {
        if (CDatas != null)
        {
            CloneDataMessage message = new CloneDataMessage
            {
                PlayerIndex = CDatas.PlayerIndex,
                CurrentRoomNumber = CDatas.CurrnetRoomNumber,
                RoomId = CDatas.RoomId,
                ImageIndex = CDatas.ImageIndex,
                Gameinventory = CDatas.Gameinventory,
                CharacterDataList = CDatas.characterDataList
            };
            NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
            CmdReciveClonT(opponentIdentity, message);
        }
    }
    /*데이터를 토대로 게임서버를 위한 GameData 값들을 설정*/
    [Command]
    void CmdReciveClonT(NetworkIdentity ClientIdentity, CloneDataMessage message)
    {
        GameObject Gameobject_Real = Instantiate(myNetworkGameRoomPrefab);
        NetworkServer.Spawn(Gameobject_Real, ClientIdentity.connectionToClient);
        GameData gameData = Gameobject_Real.GetComponent<GameData>();
        GameData_Own = gameData;
        gameData.PlayerIndex = message.PlayerIndex;
        gameData.CurrnetRoomNumber = message.CurrentRoomNumber;
        gameData.RoomId = message.RoomId;
        gameData.RoomObj = this.gameObject;
        gameData.ImageIndex = message.ImageIndex;

        gameData.Gameinventory.Clear();
        gameData.characterDataList.Clear();

        foreach (var item in message.Gameinventory)
        {
            gameData.Gameinventory.Add(item);
        }

        foreach (var characterInfo in message.CharacterDataList)
        {
            gameData.characterDataList.Add(characterInfo);
        }
        gameData.GetCloneData = true;
        RpcClonTDone(ClientIdentity.connectionToClient);
    }
    [TargetRpc]
    void RpcClonTDone(NetworkConnection target)
    {
        if (roomthings.sliderCoroutine != null)
        {
            StopCoroutine(roomthings.sliderCoroutine);
        }
        roomthings.sliderCoroutine = StartCoroutine(IncreaseSliderOverTime(0.98f, 0.5f));

        NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
        LastCheckPExist(opponentIdentity);
    }
    [Command]
    void LastCheckPExist(NetworkIdentity clientIdentity)
    {
        StartCoroutine(CheckAllPExistCorutin(clientIdentity));
    }
    /*게임서버에서 연결이 안되는 유저 확인*/
    private IEnumerator CheckAllPExistCorutin(NetworkIdentity clientIdentity)
    {
        float startTime = Time.time;
        while (!AreAllPlayersExist())
        {
            if (Time.time - startTime > 20f)
            {
                NotifyPlayersOfConnectionIssue();
                PreRoomToGame();
                RoomToGame(clientIdentity.connectionToClient);
                yield break;
            }
            yield return new WaitForSeconds(0.01f);
        }
        PreRoomToGame();
        RoomToGame(clientIdentity.connectionToClient);
    }
    /*RecivePnum과 GameData의 개수를 비교*/
    private bool AreAllPlayersExist()
    {
        Scene thisScene = gameObject.scene;
        var allGameObjects = thisScene.GetRootGameObjects();
        var allGameData = allGameObjects.SelectMany(obj => obj.GetComponentsInChildren<GameData>()).ToList();

        int expectedNumPlayers = RecivePnum <= 10 ? RecivePnum : RecivePnum - 10;
        int actualNumPlayersWithData = allGameData.Count(gameData => gameData != null && gameData.GetCloneData);

        return actualNumPlayersWithData == expectedNumPlayers;
    }
    /*클라이언트는 로딩바를 100으로 만들고 게임 씬으로 이동*/
    [TargetRpc]
    public void RoomToGame(NetworkConnection target)
    {
        if (roomthings.sliderCoroutine != null)
        {
            StopCoroutine(roomthings.sliderCoroutine);
        }
        roomthings.sliderCoroutine = StartCoroutine(IncreaseSliderOverTime(1.0f, 0.5f));
        CDatas.GamesceneAsync.allowSceneActivation = true;
    }
    void PreRoomToGame()
    {
        NetworkManager.singleton.maxConnections = NetworkManager.singleton.numPlayers;
        /*RoomCheck에서 게임서버로 이동 중 유저가 이탈 시 RpcNotifyPlayersNumber_Fail()으로 인해 RecivePnum이 10
         게임 서버에서 해당 유저가 연결 끊겼음을 저장하고 유저에게 전송*/
        if(RecivePnum >= 10)
        {
            Scene thisScene = gameObject.scene;
            var allGameObjects = thisScene.GetRootGameObjects();
            var allGameData = allGameObjects.SelectMany(obj => obj.GetComponentsInChildren<GameData>()).ToList();
            List<int> activePlayerIndexes = allGameData.Select(gameData => gameData.PlayerIndex).ToList();
            for(int i = 0; i < 4; i++)
            {
                if(!activePlayerIndexes.Contains(i))
                    GameData_Own.Gameinventory[i].PConnection = false;
            }
        }
        
    }
    /*오류 발생 로깅*/
    private void NotifyPlayersOfConnectionIssue()
    {
        Debug.Log("NotifyPlayersOfConnectionIssue");
    }
}
