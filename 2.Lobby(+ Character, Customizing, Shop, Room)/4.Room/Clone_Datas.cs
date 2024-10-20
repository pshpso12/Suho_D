using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;

public class Clone_Datas : MonoBehaviour
{
    public int PlayerIndex;
    public int CurrnetRoomNumber;
    public Guid RoomId;
    public int ImageIndex;
    public List<GameItem> Gameinventory = new List<GameItem>();
    public List<CharacterCostumeInfo> characterDataList = new List<CharacterCostumeInfo>();
    public AsyncOperation GamesceneAsync;

    void Start()
    {
        DontDestroyOnLoad(this);
    }
}
