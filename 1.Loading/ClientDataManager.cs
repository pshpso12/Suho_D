using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class ClientDataManager : MonoBehaviour
{
    public static ClientDataManager Instance { get; private set; }

    public LodingServerScript.ServerResponse UserDetails { get; private set; }
    public LodingServerScript.CharactersResponse CharacterData { get; private set; }
    public LodingServerScript.OutfitsResponse OutfitData { get; private set; }
    public LodingServerScript.AccessoriesResponse AccessoriesData { get; private set; }
    public GameData_Server.LootItemResponse LootItemData { get; private set; }
    public Texture2D UserTexture { get; set; }

    public bool ChatBan = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void UpdateUserDetails(SteamLobby_Server.ServerResponse userDetails)
    {
        UserDetails = userDetails;
    }

    public void UpdateCharacterData(SteamLobby_Server.CharactersResponse characterData)
    {
        CharacterData = characterData;
    }

    public void UpdateOutfitData(SteamLobby_Server.OutfitsResponse outfitData)
    {
        OutfitData = outfitData;
    }

    public void UpdateAccessoriesData(SteamLobby_Server.AccessoriesResponse accessoriesData)
    {
        AccessoriesData = accessoriesData;
    }
    public void UpdateUserTexture(Texture2D userTexture)
    {
        UserTexture = userTexture;
    }

    public void UpdateUserDetails_MainCha(string newIndex)
    {
        UserDetails.MainCharacterID = newIndex;
    }

    public void UpdateCharacterOutfit(int characterType, string outfitType, string newOutfitID)
    {
        var characterToUpdate = CharacterData.characters.FirstOrDefault(c => c.CharacterType == characterType);
        if (characterToUpdate != null)
        {
            switch (outfitType)
            {
                case "Top":
                    characterToUpdate.TopOutfitID = newOutfitID;
                    break;
                case "Bottom":
                    characterToUpdate.BottomOutfitID = newOutfitID;
                    break;
                case "Shoes":
                    characterToUpdate.ShoesOutfitID = newOutfitID;
                    break;
            }
        }
        else
        {
            Debug.LogError("Character with type " + characterType + " not found.");
        }
    }

    public void CostUpdate(int CP, int BP)
    {
        UserDetails.cashpoint = CP;
        UserDetails.basepoint = BP;
    }

    public void UpdateLootitemData(GameData_Server.LootItemResponse lootitemData)
    {
        LootItemData = lootitemData;
    }

    public void UpdateGameUserStats(GameData_Server.UpdateUserStatsResponse GameUserData)
    {
        UserDetails.basepoint = GameUserData.newBp;
        UserDetails.ExperiencePoints = GameUserData.newExperience;
        UserDetails.Level = GameUserData.newLevel;
    }
}
