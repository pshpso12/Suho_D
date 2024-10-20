using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using UnityEngine.Networking;
using Mirror;

public class LodingServerScript : MonoBehaviour
{
    private string serverURL = "http://localhost:XXXX";
    [SerializeField] private Loading_Server loadingserver;
    [System.Serializable]
    public class SteamIDData
    {
        public string steamID;
    }
    [System.Serializable]
    public class UserIDData
    {
        public string userID;
    }
    [System.Serializable]
    public class ServerResponse
    {
        public bool exists;
        public string message;
        public string userID;
        public string Nickname;
        public int Level;
        public int ExperiencePoints;
        public string MainCharacterID;
        public int cashpoint;
        public int basepoint;
        public bool isban;
    }
    [System.Serializable]
    public class CharacterData
    {
        public int CharacterType;
        public string TopOutfitID;
        public string BottomOutfitID;
        public string ShoesOutfitID;
        public string AllInOneOutfitID;
        public string Accessory1ID;
        public string Accessory2ID;
        public string Accessory3ID;
        public string Accessory4ID;
        public string Accessory5ID;
        public string Accessory6ID;
        public string Accessory7ID;
        public string Accessory8ID;
    }
    [System.Serializable]
    public class OutfitData
    {
        public int OutfitID;
        public string Description;
        public string Type;
        public int Character_costume;
    }
    [System.Serializable]
    public class AccessoriesData
    {
        public string Description;
        public string Type;
        public int Character_costume;
    }

    [System.Serializable]
    public class CharactersResponse
    {
        public List<CharacterData> characters;
    }
    [System.Serializable]
    public class OutfitsResponse
    {
        public List<OutfitData> outfits;
    }
    [System.Serializable]
    public class AccessoriesResponse
    {
        public List<AccessoriesData> accessories;
    }

    [System.Serializable]
    public class NicknameData
    {
        public string nickname;
    }

    [System.Serializable]
    public class UserData
    {
        public string steamID;
        public string nickname;
    }

    /*DB에서 해당 steamID로 생성된 User가 있는지 확인*/
    public IEnumerator CheckSteamID(string steamID, NetworkIdentity ClientIdentity)
    {
        SteamIDData dataToSend = new SteamIDData { steamID = steamID };
        string json = JsonUtility.ToJson(dataToSend);

        UnityWebRequest www = new UnityWebRequest(serverURL + "/check-steamID", "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        www.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
        www.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");

        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError(www.error);
        }
        else
        {
            ServerResponse response = JsonUtility.FromJson<ServerResponse>(www.downloadHandler.text);

            /*존재한다면 Ban 유저인지를 확인하고 벤유저일 경우 클라이언트 종료 명령을 보냄
             존재하고 Ban 유저가 아니라면 추가로 필요한 데이터를 가져옴
             존재하지 않는다면 exits가 false인 respone을 전송해 유저 생성을 할 수 있도록 함*/
            if(response.exists)
            {
                if(response.isban)
                {
                    if(ClientIdentity.connectionToClient != null)
                    {
                        loadingserver.isBanUser(ClientIdentity.connectionToClient);
                    }
                }
                else
                {
                    yield return StartCoroutine(GetUserDetails(steamID, ClientIdentity));
                    yield return StartCoroutine(GetDataFromTable("/get-outfit", response.userID, ClientIdentity));
                    yield return StartCoroutine(GetDataFromTable("/get-accessory", response.userID, ClientIdentity));
                    yield return StartCoroutine(GetDataFromTable("/get-characters", response.userID, ClientIdentity));
                }
            }
            else if(!response.exists)
            {
                if(ClientIdentity.connectionToClient != null)
                {
                    loadingserver.DataSendToClient(ClientIdentity.connectionToClient, response);
                }
            }
        }
        www.uploadHandler.Dispose();
    }

    /*DB에서 유니크키인 steamID를 이용해 User 테이블의 정보를 가져옴*/
    IEnumerator GetUserDetails(string steamID, NetworkIdentity ClientIdentity)
    {
        SteamIDData dataToSend = new SteamIDData { steamID = steamID };
        string json = JsonUtility.ToJson(dataToSend);

        UnityWebRequest www = new UnityWebRequest(serverURL + "/get-userdetails", "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        www.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
        www.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");

        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError(www.error);
        }
        else
        {
            ServerResponse response = JsonUtility.FromJson<ServerResponse>(www.downloadHandler.text);
            if(ClientIdentity.connectionToClient != null)
            {
                loadingserver.DataSendToClient(ClientIdentity.connectionToClient, response);
            }
        }
        www.uploadHandler.Dispose();
    }

    /*DB에서 외래키인 userID를 이용해 각 테이블의 정보를 가져옴*/
    IEnumerator GetDataFromTable(string endpoint, string userID, NetworkIdentity ClientIdentity)
    {
        UserIDData dataToSend = new UserIDData { userID = userID };
        string json = JsonUtility.ToJson(dataToSend);

        UnityWebRequest www = new UnityWebRequest(serverURL + endpoint, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        www.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
        www.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");

        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError(www.error);
        }
        else
        {
            if(endpoint == "/get-characters")
            {
                CharactersResponse charactersResponse = JsonUtility.FromJson<CharactersResponse>(www.downloadHandler.text);
                if(ClientIdentity.connectionToClient != null)
                {
                    loadingserver.CharacterDataSendToClient(ClientIdentity.connectionToClient, charactersResponse);
                }
            }
            else if(endpoint == "/get-outfit")
            {
                OutfitsResponse outfitsResponse = JsonUtility.FromJson<OutfitsResponse>(www.downloadHandler.text);
                if(ClientIdentity.connectionToClient != null)
                {
                    loadingserver.OutfitDataSendToClient(ClientIdentity.connectionToClient, outfitsResponse);
                }
            }
            else if(endpoint == "/get-accessory")
            {
                AccessoriesResponse accessoriesResponse = JsonUtility.FromJson<AccessoriesResponse>(www.downloadHandler.text);
                if(ClientIdentity.connectionToClient != null)
                {
                    loadingserver.AccDataSendToClient(ClientIdentity.connectionToClient, accessoriesResponse);
                }
            }
        }
        www.uploadHandler.Dispose();
    }

    /*DB에서 동일한 닉네임이 있는지 여부를 확인*/
    public IEnumerator CheckNickname(string check_nickname, NetworkIdentity ClientIdentity)
    {
        NicknameData dataToSend = new NicknameData { nickname = check_nickname };
        string json = JsonUtility.ToJson(dataToSend);

        UnityWebRequest www = new UnityWebRequest(serverURL + "/check-nickname", "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        www.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
        www.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");

        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError(www.error);
        }
        else
        {
            ServerResponse response = JsonUtility.FromJson<ServerResponse>(www.downloadHandler.text);

            if(response.exists)
            {
                if(ClientIdentity.connectionToClient != null)
                {
                    loadingserver.TargetNicknameSubmissionResult_fail(ClientIdentity.connectionToClient, response, check_nickname);
                }
            }
            else
            {
                if(ClientIdentity.connectionToClient != null)
                {
                    loadingserver.TargetNicknameSubmissionResult(ClientIdentity.connectionToClient, response, check_nickname);
                }
            }
        }
        www.uploadHandler.Dispose();
    }

    /*steamID와 닉네임을 토대로 유저 create를 진행*/
    public IEnumerator CreateNewUser(string nickname, string steamID, NetworkIdentity ClientIdentity)
    {
        UserData dataToSend = new UserData { steamID = steamID, nickname = nickname };
        string json = JsonUtility.ToJson(dataToSend);

        UnityWebRequest www = new UnityWebRequest(serverURL + "/add-user", "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        www.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
        www.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");

        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError(www.error);
        }
        else
        {
            /*유저, 캐릭터, 의상, 악세사리 데이터를 불러오기 위해 CheckSteamID 다시 진행*/
            yield return StartCoroutine(CheckSteamID(steamID, ClientIdentity));
            loadingserver.CreateDone(ClientIdentity.connectionToClient);
        }

        www.uploadHandler.Dispose();
    }
    
}
