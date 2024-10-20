using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class ShopCharacter : MonoBehaviour
{
    public Button[] ChaButtons;
    public GameObject[] characterPrefabs;
    public Transform characterSpawnPoint;
    private GameObject currentCharacter;
    
    public ShopBase shopbase;

    public float rotationSpeed = 500.0f;
    private bool isDragging = false;

    public int currentChaNum;
    public Button ReloadBtn;

    private Camera cam;

    void Start()
    {   
        if(int.TryParse(ClientDataManager.Instance.UserDetails.MainCharacterID, out int characterID))
        {
            CreateCharacter(characterID);
        }

        for (int i = 0; i < ChaButtons.Length; i++)
        {
            int index = i;
            ChaButtons[i].onClick.AddListener(() => OnCharacterButtonClicked(index + 1));
        }
        /*상점에서 의상을 입혀봤다가 다시 기존 옷으로 입히기 위함*/
        ReloadBtn.onClick.AddListener(() => ReloadaCharacter(currentChaNum));

        cam = GameObject.Find("Main Camera").GetComponent<Camera>();
        AdjustFOV();
    }
    /*상점에서 캐릭터가 좌측에 배치되어 되어있는데 비율에 따라 캐릭터가 잘리는 것을 막기 위해 비율에 따른 화면 조정*/
    void AdjustFOV()
    {
        float aspectRatio = Screen.width / (float)Screen.height;

        if (aspectRatio > 1.7) // 16:9
        {
            cam.fieldOfView = 60;
        }
        else if (aspectRatio > 1.5) // 16:10
        {
            cam.fieldOfView = 63;
        }
        else if (aspectRatio <= 1.5 && aspectRatio > 1.3) // 4:3
        {
            cam.fieldOfView = 73;
        }
        else if (aspectRatio <= 1.3 && aspectRatio > 1.2) // 5:4
        {
            cam.fieldOfView = 73;
        }
    }
    /*캐릭터 이미지 클릭 시 해당 캐릭터 배치 및 해당 캐릭터에 맞는 의상 배치*/
    void OnCharacterButtonClicked(int characterIndex)
    {
        CreateCharacter(characterIndex);
        shopbase.CharacterNum = characterIndex;
        shopbase.ActivateObject(0);
    }

    void CreateCharacter(int characterID)
    {
        if (currentCharacter != null)
            Destroy(currentCharacter);

        currentChaNum = characterID;
        if (characterID > 0 && characterID <= characterPrefabs.Length)
        {
            GameObject prefab = characterPrefabs[characterID - 1];
            currentCharacter = Instantiate(prefab, characterSpawnPoint.position, characterSpawnPoint.rotation);
            currentCharacter.transform.localScale = characterSpawnPoint.localScale;
        }
        /*기존 해당 캐릭터 의상 적용*/
        foreach (var characterData in ClientDataManager.Instance.CharacterData.characters)
        {
            if (characterData.CharacterType == characterID)
            {
                if (characterData.TopOutfitID != "")
                    ApplyOutfit(characterData.TopOutfitID, currentCharacter, "Top", characterID);
                if (characterData.BottomOutfitID != "")
                    ApplyOutfit(characterData.BottomOutfitID, currentCharacter, "Bottom", characterID);
                if (characterData.ShoesOutfitID != "")
                    ApplyOutfit(characterData.ShoesOutfitID, currentCharacter, "Shoes", characterID);
                if (characterData.AllInOneOutfitID != "")
                    ApplyOutfit(characterData.AllInOneOutfitID, currentCharacter, "All_in_one", characterID);
                if (characterData.Accessory1ID == "")
                    Debug.Log("ACC1ID is Null!!");

                break;
            }
        }
    }

    void ApplyOutfit(string outfitID, GameObject character, string outType, int characterNum)
    {
        if(int.TryParse(outfitID, out int outfitIDin))
        {
            foreach (var outfit in ClientDataManager.Instance.OutfitData.outfits)
            {
                if (outfit.OutfitID == outfitIDin && outfit.Type == outType && outfit.Character_costume == characterNum)
                {
                    Transform categoryTransform = character.transform.Find(outType);
                    if (categoryTransform != null)
                    {
                        foreach (Transform child in categoryTransform)
                        {
                            bool shouldEnable = child.name == outfit.Description;
                            child.gameObject.SetActive(shouldEnable);
                        }
                    }
                }
            }
        }
    }
    /*ShopBase에서 사용(의상 변경)*/
    public void ChnageCharacterOutfit(string OutfitType, string OutfitName)
    {
        if(currentCharacter != null)
        {
            Transform categoryTransform = currentCharacter.transform.Find(OutfitType);
            if (categoryTransform != null)
            {
                foreach (Transform child in categoryTransform)
                {
                    bool shouldEnable = child.name == OutfitName;
                    child.gameObject.SetActive(shouldEnable);
                }
            }
        }
    }
    /*캐릭터 회전 값 초기화 및 의상 적용*/
    public void ReloadaCharacter(int characterID)
    {
        if(currentCharacter != null)
        {
            StartCoroutine(RotateCharacterToSpawnPoint(currentCharacter, characterSpawnPoint.rotation, 0.3f));
            foreach (var characterData in ClientDataManager.Instance.CharacterData.characters)
            {
                if (characterData.CharacterType == characterID)
                {
                    if (characterData.TopOutfitID != "")
                        ApplyOutfit(characterData.TopOutfitID, currentCharacter, "Top", characterID);
                    if (characterData.BottomOutfitID != "")
                        ApplyOutfit(characterData.BottomOutfitID, currentCharacter, "Bottom", characterID);
                    if (characterData.ShoesOutfitID != "")
                        ApplyOutfit(characterData.ShoesOutfitID, currentCharacter, "Shoes", characterID);
                    if (characterData.AllInOneOutfitID != "")
                        ApplyOutfit(characterData.AllInOneOutfitID, currentCharacter, "All_in_one", characterID);
                    if (characterData.Accessory1ID == "")
                        Debug.Log("ACC1ID is Null!!");

                    break;
                }
            }
        }
    }
    /*회전 값이 바로 변경 시 시각적으로 좋지 않아, 천천히 이동하게 변경*/
    private IEnumerator RotateCharacterToSpawnPoint(GameObject character, Quaternion targetRotation, float duration)
    {
        float time = 0;
        Quaternion startRotation = character.transform.rotation;

        while (time < duration)
        {
            character.transform.rotation = Quaternion.Lerp(startRotation, targetRotation, time / duration);
            time += Time.deltaTime;
            yield return null;
        }

        character.transform.rotation = targetRotation;
    }
    /*캐릭터 회전을 위해 이용*/
    void Update()
    {
        if (isDragging && currentCharacter != null)
        {
            float rotationX = Input.GetAxis("Mouse X") * rotationSpeed * Time.deltaTime;
            currentCharacter.transform.Rotate(Vector3.up, -rotationX);
        }
    }
    /*Pointer Down 이벤트로 적용*/
    public void StartRotation()
    {
        isDragging = true;
    }
    /*Pointer Up 이벤트로 적용*/
    public void StopRotation()
    {
        isDragging = false;
    }
}
