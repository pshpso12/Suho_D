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
using Insight;

/*동작을 쉽게 보기위해 포트폴리오에서는 서버동작과 클라이언트동작을 합쳐두었습니다.*/
/*실제로는 [TargetRpc]가 Loading_Client에서 동작합니다.*/
public class Loading_Server : NetworkBehaviour
{
    [SerializeField] private LodingServerScript lodingserverscript;
    [SerializeField] private string GameVersionString;

    /*클라이언트로 부터 버전 정보를 받고 동일한지 확인*/
    [Command]
    private void CmdSendGameVersionString(string GVersion)
    {
        if(GameVersionString == GVersion)
        {
            RpcVersionMatch(1);
        }
        else
        {
            RpcVersionMatch(2);
        }
    }
    /*클라이언트의 변수가 변경되고 CheckGameVersion()에서 이를 확인*/
    [TargetRpc]
    public void RpcVersionMatch(int isVersionMatch)
    {
        GameVersionCheck = isVersionMatch;
    }
    /*SteamID를 클라이언트로 부터 받아, 해당 SteamID가 DB에 존재하는지 확인*/
    [Command]
    private void CmdSendSteamData(string steamID, NetworkIdentity ClientIdentity)
    {
        lodingserverscript.StartCoroutine(lodingserverscript.CheckSteamID(steamID, ClientIdentity));
    }
    /*LoadingServerScript를 통해 벤 유저인 경우 클라이언트는 프로그램을 종료*/
    [TargetRpc]
    public void isBanUser(NetworkConnectionToClient target)
    {
        Application.Quit();
    }
    /*클라이언트는 정보를 respone 정보를 저장하고, isUserDataUpdated_1 변수를 true로 설정
    클라이언트는 Update()에서 다음을 확인하고 HandleLoadingProcess()를 진행*/
    [TargetRpc]
    public void DataSendToClient(NetworkConnectionToClient target, SteamLobby_Server.ServerResponse response)
    {
        ClientDataManager.Instance.UpdateUserDetails(response);
        isUserDataUpdated_1 = true;
    }
    /*클라이언트는 캐릭터 상태를 저장*/
    [TargetRpc]
    public void CharacterDataSendToClient(NetworkConnectionToClient target, SteamLobby_Server.CharactersResponse response)
    {
        ClientDataManager.Instance.UpdateCharacterData(response);
    }
    /*클라이언트는 보유 의상 정보를 저장*/
    [TargetRpc]
    public void OutfitDataSendToClient(NetworkConnectionToClient target, SteamLobby_Server.OutfitsResponse response)
    {
        ClientDataManager.Instance.UpdateOutfitData(response);
    }
    /*클라이언트는 보유 악세사리 정보를 저장*/
    [TargetRpc]
    public void AccDataSendToClient(NetworkConnectionToClient target, SteamLobby_Server.AccessoriesResponse response)
    {
        ClientDataManager.Instance.UpdateAccessoriesData(response);
    }   
    /*서버는 닉네임을 받아 중복된 닉네임이 DB에 있는 확인을 진행*/
    [Command]
    private void CmdSubmitNickname(string nickname, NetworkIdentity ClientIdentity)
    {
        lodingserverscript.StartCoroutine(lodingserverscript.CheckNickname(nickname, ClientIdentity));
    }
    /*중복된 닉네임이 없다면 클라이언트는 사용할 수 있음을 확인하고 승인 혹은 거절 가능*/
    [TargetRpc]
    public void TargetNicknameSubmissionResult(NetworkConnectionToClient target, SteamLobby_Server.ServerResponse response, string rechecknick)
    {
        loadingthings.nickSuccessText.text = $"이 닉네임 은 사용할 수 있습니다.\n<b>{rechecknick}</b>으로 생성하시겠습니까?";
        loadingthings.Nick_Logsuccess.SetActive(true);
        EventSystem.current.SetSelectedGameObject(Nick_Logsuccess1_btn.gameObject);
    }
    /*중복된 닉네임이 있음을 클라이언트에게 고지*/
    [TargetRpc]
    public void TargetNicknameSubmissionResult_fail(NetworkConnectionToClient target, SteamLobby_Server.ServerResponse response, string rechecknick)
    
        loadingthings.Nick_Logfail.SetActive(true);
        EventSystem.current.SetSelectedGameObject(loadingthings.Nick_Logfail1_btn.gameObject);
        loadingthings.Nickname_Field.text = "";
        if(UisoundManager != null)
            UisoundManager.PlayWarringSound();
    }
    /*서버는 최종적으로 닉네임과 SteamID를 받아 유저 생성을 진행*/
    [Command]
    private void CmdCreateNewUser(string nickname, string steamID, NetworkIdentity ClientIdentity)
    {
        lodingserverscript.StartCoroutine(lodingserverscript.CreateNewUser(nickname, steamID, ClientIdentity));
    }
    /*유저 생성이 정상적으로 완료되었으면, 비동기로 로드한 씬을 완료하여 Lobby로 이동*/
    [TargetRpc]
    public void CreateDone(NetworkConnectionToClient target)
    {
        sceneAsync.allowSceneActivation = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
        
    }
    /*귓속말 시스템을 위해 서버에서 닉네임 저장*/
    [Command]
    private void CmdServerSaveNick(string nickname, string steamid)
    {
        NetworkConnectionToClient conn = connectionToClient;
        if (conn.identity != null)
        {
            ServerDataManger playerData = conn.identity.GetComponent<ServerDataManger>();
            if (playerData != null)
            {
                playerData.playerName = nickname;
                playerData.SteamID_ = steamid;
            }
        }
    } 
}
