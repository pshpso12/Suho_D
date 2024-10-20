using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;

public class Room
{
    public Guid Id;
    public int RoomNumber;
    public string RoomName;
    public int MaxRoomPNumber;
    public int CurrentRoomPNumber;
    public string Password;
    public Scene RoomScene;
    public List<bool> PlayerSlots = new List<bool> { false, false, false, false };
    public bool RoomStart;
}

public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance { get; private set; }
    
    public List<Room> rooms = new List<Room>();
    public Queue<int> availableRoomNumbers = new Queue<int>();
    private List<string> profanitiesList = new List<string>();
    public List<string> RoomNameList = new List<string>();
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        string[] profanities = Resources.Load<TextAsset>("profanities").text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        profanitiesList.AddRange(profanities);
    }

    public void CreateRoom(string name, int capacity, string password = "", Action<Room, Scene> onCompleted = null)
    {
        int roomNumber;
        /*방 번호는 계속 증가하는 것을 방지하기 위해 Dequeue와 Enqueue활용*/
        if (availableRoomNumbers.Count > 0)
        {
            roomNumber = availableRoomNumbers.Dequeue();
        }
        else
        {
            roomNumber = rooms.Count + 1;
        }
        /*욕설 체크만을 위해 띄어쓰기 제거, 욕설이 있으면 이름은 공백으로 정의*/
        string nameForProfanityCheck = name.Replace(" ", "");
        foreach (var profanity in profanitiesList)
        {
            if (nameForProfanityCheck.Contains(profanity, StringComparison.OrdinalIgnoreCase))
            {
                name = "";
                break;
            }
        }
        /*공백이면 미리 정의해둔 5개의 방 이름 중 랜덤한 하나로 설정*/
        if (string.IsNullOrWhiteSpace(name))
        {
            int randomIndex = UnityEngine.Random.Range(0, RoomNameList.Count);
            name = RoomNameList[randomIndex];
        }
        /*위의 정보들로 새로운 방 생성*/
        Room newRoom = new Room
        {
            Id = Guid.NewGuid(),
            RoomNumber = roomNumber,
            RoomName = name,
            MaxRoomPNumber = capacity,
            Password = password,
            CurrentRoomPNumber = 1,
            RoomStart = false
        };

        /*방 생성 시 생성한 유저는 0번으로 지정하고 해당 값으로 방에 입장하는 것을 방지*/
        newRoom.PlayerSlots[0] = true;
        
        rooms.Add(newRoom);
        /*Master서버에서는 방을 Additive Scene을 이용해 구분
        절차적으로 씬이 로드된 후, 방이 어떤 씬인지를 저장, onCompleted를 완료하여 다음을 진행함*/
        StartCoroutine(LoadRoomScenes(newRoom.RoomNumber, loadedScene => {
            newRoom.RoomScene = loadedScene;
            onCompleted?.Invoke(newRoom, loadedScene);
        }));
    }

    /*방 삭제*/
    public void DeleteRoom(int roomNumber)
    {
        rooms.RemoveAll(room => room.RoomNumber == roomNumber);
        availableRoomNumbers.Enqueue(roomNumber);
    }

    public List<Room> GetRooms()
    {
        return rooms;
    }

    /*Additive로 씬 로드*/
    IEnumerator LoadRoomScenes(int roomNumber, Action<Scene> onSceneLoaded)
    {
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync("ServerRoom_Scene", LoadSceneMode.Additive);
        yield return asyncLoad;

        Scene loadedScene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
        onSceneLoaded?.Invoke(loadedScene);
    }

    /*방을 생성 후 이름을 변경할 때 이용*/
    public string CheckRoomName(string NewRoomName)
    {
        string nameForProfanityCheck = NewRoomName.Replace(" ", "");
        foreach (var profanity in profanitiesList)
        {
            if (nameForProfanityCheck.Contains(profanity, StringComparison.OrdinalIgnoreCase))
            {
                NewRoomName = "";
                break;
            }
        }
        if (string.IsNullOrWhiteSpace(NewRoomName))
        {
            int randomIndex = UnityEngine.Random.Range(0, RoomNameList.Count);
            NewRoomName = RoomNameList[randomIndex];
        }
        return NewRoomName;
    }
}
