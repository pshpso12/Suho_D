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

public class RoomCheck : NetworkBehaviour
{
    void Start()
    {
        if(isServer)
            StartCoroutine(SpawnProcessCheck());
    }
    /*서버 측에서 모든 유저가 준비되었는지를 확인하기 위함*/
    IEnumerator SpawnProcessCheck()
    {
        float startTime = Time.time;
        while (!AreAllPlayersReady())
        {
            if (Time.time - startTime > 70f)
            {
                RpcNotifyAllPlayersNoneReady();
                yield break;
            }
            yield return new WaitForSeconds(0.1f);
        }
        RpcNotifyAllPlayersReady();
    }

    private bool AreAllPlayersReady()
    {
        Scene thisScene = gameObject.scene;
        var allGameObjects = thisScene.GetRootGameObjects();
        int roomDataCount = allGameObjects.Count(obj => obj.GetComponent<RoomData>() != null);
        int RacketCount = allGameObjects.Count(obj => obj.GetComponent<Lobby>() != null);
        /*방 오브젝트와 로비 오브젝트의 개수 동일한지 확인하고 모든 RoomData의 준비 값을 확인*/
        if (roomDataCount == RacketCount)
        {
            var allGameData = allGameObjects.SelectMany(obj => obj.GetComponentsInChildren<RoomData>()).ToList();
            foreach (RoomData gameData in allGameData)
            {
                if (gameData == null || !gameData.GetRoomData || !gameData.GetCharacters || !gameData.GetGameScene)
                {
                    return false;
                }
            }
            return true;
        }
        else
        {
            return false;
        }
    }
    /*모두 준비완료된 경우 GameCheckDone()을 진행*/
    private void RpcNotifyAllPlayersReady()
    {
        Scene targetScene = gameObject.scene;
        foreach (GameObject obj in targetScene.GetRootGameObjects())
        {
            RoomData roomData = obj.GetComponentInChildren<RoomData>(true);
            if (roomData != null)
            {
                NetworkIdentity targetIdentity = roomData.gameObject.GetComponent<NetworkIdentity>();
                if(targetIdentity != null && targetIdentity.connectionToClient != null)
                {
                    /*클라이언트는 로딩바를 채우고 서버로 게임서버로 이동을 요청*/
                    roomData.GameCheckDone(targetIdentity.connectionToClient, roomData.NetUniqueID);
                }
            }
        }
        StartCoroutine(SpawnPlayerCheck());
    }

    /*게임 준비가 실패*/
    private void RpcNotifyAllPlayersNoneReady()
    {
        Scene targetScene = gameObject.scene;
        foreach (GameObject obj in targetScene.GetRootGameObjects())
        {
            RoomData roomData = obj.GetComponentInChildren<RoomData>(true);
            if (roomData != null)
            {
                NetworkIdentity targetIdentity = roomData.gameObject.GetComponent<NetworkIdentity>();
                if(targetIdentity != null && targetIdentity.connectionToClient != null)
                {
                    /*클라이언트 로딩창 끄고 로딩 관련 값 초기화*/
                    roomData.GameCheckFail(targetIdentity.connectionToClient);
                }
                /*방에서 시작을 false로 변경하여 방에 입장 가능하게 변경*/
                Room roomToChange = RoomManager.Instance.rooms.FirstOrDefault(room => room.RoomNumber == roomData.CurrnetRoomNumber);
                if (roomToChange != null && roomToChange.Id == roomData.RoomId && roomToChange.RoomStart)
                {
                    roomToChange.RoomStart = false;
                }
            }
        }
        Destroy(this.gameObject);
    }

    IEnumerator SpawnPlayerCheck()
    {
        float startTime = Time.time;
        while (!ArePlayerNum())
        {
            if (Time.time - startTime > 30f)
            {
                RpcNotifyPlayersNumber_Fail();
                yield break;
            }
            yield return new WaitForSeconds(0.1f);
        }
        RpcNotifyPlayersNumber_Done();
    }
    /*isDummy는 Master서버 측에 Game서버로 이동하기전에 어느 방에 있고 어느 게임서버로 이동했는지르 저장해둔 경우 true*/
    private bool ArePlayerNum()
    {
        Scene thisScene = gameObject.scene;
        var allGameObjects = thisScene.GetRootGameObjects();
        var allGameData = allGameObjects.SelectMany(obj => obj.GetComponentsInChildren<RoomData>()).ToList();
        foreach (RoomData gameData in allGameData)
        {
            if (!gameData.isDummy)
            {
                return false;
            }
        }
        return true;
    }
    /*서버측에 모두 저장된 경우 netUniqueID는 게임서버의 port*/
    private void RpcNotifyPlayersNumber_Done()
    {
        Scene thisScene = gameObject.scene;
        var allGameObjects = thisScene.GetRootGameObjects();
        var allGameData = allGameObjects.SelectMany(obj => obj.GetComponentsInChildren<RoomData>()).ToList();
        int count = 0;
        string netUniqueID= null;
        foreach (RoomData gameData in allGameData)
        {
            count++;
            netUniqueID = gameData.NetUniqueID;
        }
        /*게임 서버 측은 게임서버에 몇명이 들어와야 게임을 시작해야하는지 모르기 때문에 입장하는 유저의 값을 게임서버로 보내줌*/
        var ServerGameManager = FindObjectOfType<Insight.ServerGameManager_>();
        if (netUniqueID != null && ServerGameManager.GetGameByUniqueID(netUniqueID) != null)
        {   
            ServerGameManager.SendPlayerNum(ServerGameManager.GetGameByUniqueID(netUniqueID).connectionId ,count);
        }
        Destroy(this.gameObject);
    }
    /*isDummy가 없는 경우 해당 유저를 로비로 보내고 남은 유저들 끼리 게임서버에 입장*/
    private void RpcNotifyPlayersNumber_Fail()
    { 
        Scene thisScene = gameObject.scene;
        var allGameObjects = thisScene.GetRootGameObjects();
        var allGameData = allGameObjects.SelectMany(obj => obj.GetComponentsInChildren<RoomData>()).ToList();
        int count = 10;
        string netUniqueID= null;
        foreach (RoomData gameData in allGameData)
        {
            if(!gameData.isDummy)
            {
                gameData.HandleDisconnection();
            }
            else
            {
                count++;
                netUniqueID = gameData.NetUniqueID;
            }
        }
        var ServerGameManager = FindObjectOfType<Insight.ServerGameManager_>();
        if (netUniqueID != null && ServerGameManager.GetGameByUniqueID(netUniqueID) != null)
        {   
            ServerGameManager.SendPlayerNum(ServerGameManager.GetGameByUniqueID(netUniqueID).connectionId ,count);
        }
        Destroy(this.gameObject);
    }
}
