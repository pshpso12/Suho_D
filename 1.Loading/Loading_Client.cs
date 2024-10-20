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

public class Loading_Client : NetworkBehaviour
{
    private Loading_Things loadingthings;
    private List<string> profanitiesList = new List<string>();
    
    public class TextureDataHolder
    {
        public static Texture2D UserTexture { get; set; }
    }
    
    public static class SteamInfo
    {
        public static string SteamID { get; set; }
        public static string Nickname { get; set; }
    }
    
    private bool isSceneLoaded = false;
    private bool isUserDataUpdated_1 = false;

    private bool coroutineStarted = false;
    private Fade_InOut fade_inout;

    private AsyncOperation sceneAsync;
    private ClientAuthentication clientAuth;

    public UISoundManager UisoundManager;
    [SerializeField] private Texture2D cursorTexture;

    [SerializeField] private string GameVersionString;
    private int GameVersionCheck = 0;
    
    void Start()
    {
        if(isClient)
            DontDestroyOnLoad(this);
        if (isClient && isLocalPlayer && SceneManager.GetActiveScene().name == "M_Loading")
        {
	    /*모든 변수 할당을 Start()문에서 Find, GetComponent()로 할당하였다가 런타임 성능을 고려하여
	    에디터에서 변수들을 직접 할당한 Loading_Things를 참조하는 방식으로 변경*/
    	    loadingthings = GameObject.Find("Loading_Object").GetComponent<Loading_Things>();
            
	    if (cursorTexture != null)
            {
		/*Default Cursor 사용 시 인게임에서 cusror 애니메이션 사용 시 
		Default Cursor가 한 프레임 적용되서 새로운 씬 마다 SetCursor 이용*/
                Cursor.SetCursor(cursorTexture, Vector2.zero, CursorMode.Auto);
            }
            
            /*Log 팝업, 닉네임 설정 버튼 - 간소화 필요*/
            loadingthings.Load_fail1_btn.onClick.AddListener(OnLoadfail1Clicked);
            loadingthings.Load_fail2_btn.onClick.AddListener(OnLoadfail2Clicked);
            loadingthings.Nick_Logfail1_btn.onClick.AddListener(OnfailLoggingClicked1);
            loadingthings.Nick_Logfail2_btn.onClick.AddListener(OnfailLoggingClicked2);
            loadingthings.Nick_Logsuccess1_btn.onClick.AddListener(OnSuccessLoggingClicked1);
            loadingthings.Nick_Logsuccess2_btn.onClick.AddListener(OnSuccessLoggingClicked2);
            loadingthings.Nickbtn.onClick.AddListener(OnNicknameSubmitClicked);
            
            /*버튼 Over, Click에 대한 소리 추가*/
            UisoundManager = GameObject.Find("UI_SoundObject").GetComponent<UISoundManager>();
            AddButtonListeners(loadingthings.Load_fail1_btn, false, true, 3);
            AddButtonListeners(loadingthings.Load_fail2_btn, false, true, 2);
            AddButtonListeners(loadingthings.Nickbtn, false, true, 3);
            AddButtonListeners(loadingthings.Nick_Logfail1_btn, false, true, 3);
            AddButtonListeners(loadingthings.Nick_Logfail2_btn, false, true, 3);
            AddButtonListeners(loadingthings.Nick_Logsuccess1_btn, false, true, 3);
            AddButtonListeners(loadingthings.Nick_Logsuccess2_btn, false, true, 2);
						
	    /*닉네임 비속어 체크를 위한 리소스 로드*/
            LoadProfanities();
						
	    /*Steam 확인*/
            if (!SteamManager.Initialized)
            {
                Debug.LogError("Steam is NOT initialized");
                /*Steam 확인이 안될 경우 Coroutine을 통해 게임을 종료함*/
                StartCoroutine(CheckGameVersion());
                return;
            }
            
            CSteamID steamID = SteamUser.GetSteamID();
            SteamInfo.SteamID = steamID.ToString();
            NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
            
            /*Steam 고유 ID를 서버로 전송*/
            CmdSendSteamData(SteamInfo.SteamID, opponentIdentity);
            
            /*clientAuth는 MasterServer와 GameServer를 계속 이동하는
            유저의 정보를 MasterServer에서 보유하고 있기 위해 이용
            예시로, MasterServer 혹은 GameServer에 유저가 접속 중인 경우에
            다시 접속이 연결될 경우 기존 접속을 끊고 접속을 시도할 수 있음*/
            GameObject clientAuthGameObject = GameObject.Find("GameClient(InsightClient)/ClientAuthentication");
            clientAuth = clientAuthGameObject.transform.GetComponent<ClientAuthentication>();
            if(!clientAuth)
            {
                Debug.LogError("lientAuth is NOT initialized");
                return;
            }
            clientAuth.SendLoginMsg(steamID.ToString(), "");
						
	    /*Cliet의 버전과 Server의 버전이 동일한지를 확인하기 위해 
	    현재 Client의 버전을 서버로 전송*/
            CmdSendGameVersionString(GameVersionString);
						
	    /*Steam에서 유저가 해당 게임을 보유 중인지 확인*/
            AppId_t gameAppID = new AppId_t(XXXXXXXX);
            bool ownsGame = SteamApps.BIsSubscribedApp(gameAppID);
            if (!ownsGame)
            {
                Debug.LogError("Player does not own this game!");
                return;
            }
            /*Steam의 아바타 이미지를 게임에서 그대로 사용하기 위해 이미지를 가져와 적용
            이 때, FlipTextureVertically를 통해 뒤집혀진 이미지를 정상적으로 수정*/
            int avatarInt = SteamFriends.GetLargeFriendAvatar(SteamUser.GetSteamID());
            Texture2D texture = null;
            if (avatarInt > 0)
            {
                uint width, height;
                SteamUtils.GetImageSize(avatarInt, out width, out height);

                byte[] avatarStream = new byte[4 * (int)width * (int)height];
                SteamUtils.GetImageRGBA(avatarInt, avatarStream, 4 * (int)width * (int)height);

                texture = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, false);
                texture.LoadRawTextureData(avatarStream);
                texture.Apply();

                FlipTextureVertically(texture);
                TextureDataHolder.UserTexture = texture;
                ClientDataManager.Instance.UpdateUserTexture(texture);
            }
            StartCoroutine(CheckGameVersion());
        }
    }
    
    /*PointerEnter, OnClick시 사운드 적용*/
    void AddButtonListeners(Button button, bool enableMouseOverSound, bool enableClickSound, int playNextSound = 0)
    {
        if (enableMouseOverSound)
        {
            EventTrigger trigger = button.gameObject.AddComponent<EventTrigger>();
            EventTrigger.Entry entry = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerEnter
            };
            entry.callback.AddListener((eventData) => {
                if (button.interactable)
                {
                    UisoundManager?.PlayMouseOverSound();
                }
            });
            trigger.triggers.Add(entry);
        }
        if (enableClickSound)
        {
            button.onClick.AddListener(() => {
                if (button.interactable)
                {
                    if (playNextSound == 0)
                    {
                        UisoundManager?.PlayClick_NextSound();
                    }
                    else if (playNextSound == 1)
                    {
                        UisoundManager?.PlayClick_NoneSound();
                    }
                    else if (playNextSound == 2)
                    {
                        UisoundManager?.PlayCancelSound();
                    }
                    else if (playNextSound == 3)
                    {
                        UisoundManager?.PlaySuccessBtnSound();
                    }
                }
            });
        }
    }
    
    /*뒤집힌 이미지 수정*/
    void FlipTextureVertically(Texture2D original)
    {
        var originalPixels = original.GetPixels();
        Color[] newPixels = new Color[originalPixels.Length];

        int width = original.width;
        int rows = original.height;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                newPixels[x + y * width] = originalPixels[x + (rows - y - 1) * width];
            }
        }

        original.SetPixels(newPixels);
        original.Apply();
    }
    
    /*클라이언트 접속시 수행하는 작업*/
    private IEnumerator CheckGameVersion()
    {
	/*로고 동영상이 4초 가량 재생된 후에 작업이 시작되어야하기 때문에
	4.5f를 기다린 후 로고 동영상 오브젝트 끄기*/
        yield return new WaitForSeconds(4.5f);
        loadingthings.panel_Logo.SetActive(false);
        
        /*서버와 통신을 기다릴 수 있게 5초의 시간 조정
        GameVersionCheck == 1은 버전이 동일하기 때문에 다음 Chek로 이동
        GameVersionCheck == 2는 버전이 다르기 때문에 WarringSound 재생 및 Log 팝업 띄우기
        GameVersionCheck == 0은 서버와 통신이 안된 것이므로 프로그램 종료*/
        float timeout = 5f;
        while (GameVersionCheck == 0 && timeout > 0)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }
        if(GameVersionCheck == 1)
        {
            StartCoroutine(CheckExist());
        }
        else if(GameVersionCheck == 2)
        {
            loadingthings.Version_fail.SetActive(true);
            if(UisoundManager != null)
                UisoundManager.PlayWarringSound();
        }
        else if(GameVersionCheck == 0)
        {
            Application.Quit();
        }
    }
    
    /*기존 접속이 있는지 여부 확인*/
    private IEnumerator CheckExist()
    {
	/*화면 전환이 바로 이루어질 경우 시각적으로 부자연스러워 검은화면 alpha 값을 
	통해 Fadein 효과 추가*/
        float alpha = loadingthings.panel_First.GetComponentInChildren<Image>().color.a;
        Color fadeColor = Color.black;
        while (alpha > 0)
        {
            yield return new WaitForSeconds(0.01f);
            alpha -= 0.01f * 2f;
            fadeColor.a = alpha;
            loadingthings.panel_First.GetComponentInChildren<Image>().color = fadeColor;

            if (fadeColor.a <= 0)
            {
                loadingthings.panel_First.SetActive(false);
            }
        }
        
        /*기존 접속확인
        "LogSuccess"는 기존 접속이 없으므로 씬 비동기로드 진행
        "LogFail"은 기존 접속이 있으므로 팝업을 띄워 기존 접속을 끊고 접속할지 접속을 종료할지 확인
        "LogNone"은 TimeOut으로 접속 종료*/
        float timeout = 20f;
        while (clientAuth.loginRes == "None" && timeout > 0)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }
        if(clientAuth.loginRes == "LogSuccess")
        {
            StartCoroutine(LoadSceneCoroutine("Lobby"));
        }
        else if(clientAuth.loginRes == "LogFail")
        {
            loadingthings.Load_fail.SetActive(true);
            clientAuth.loginRes = "None";
            if(UisoundManager != null)
                UisoundManager.PlayWarringSound();
        }
        else if(clientAuth.loginRes == "None")
        {
            Application.Quit();
        }
    }
    /*비동기 씬로드 및 로딩바 채우기*/
    private IEnumerator LoadSceneCoroutine(string sceneName)
    {
        yield return new WaitForSeconds(0.5f);
        sceneAsync = SceneManager.LoadSceneAsync(sceneName);
        sceneAsync.allowSceneActivation = false; 
        while (!sceneAsync.isDone)
        {
            float LoadingNum = sceneAsync.progress * 0.9f;
            loadingthings.LoadingSldier.value = sceneAsync.progress * 0.9f;
            loadingthings.LoadingSlider_Text.text = (LoadingNum * 100).ToString("F0") + " %";
            if (sceneAsync.progress >= 0.9f)
            {
                isSceneLoaded = true;
                break;
            }
            yield return null;
        }
    }

    void Update()
    {
	/*씬 로드 완료, 유저정보 불러오기 완료 시를 확인 후 다음 진행*/
        if (isSceneLoaded && isUserDataUpdated_1 && !coroutineStarted)
        {
            coroutineStarted = true;
            StartCoroutine(HandleLoadingProcess());   
        }
    }
    
    private IEnumerator HandleLoadingProcess()
    {
	/*유저정보가 있는 경우 로비 씬으로 이동*/
        if (ClientDataManager.Instance.UserDetails.exists)
        {
	    /*FillSlider와 로딩바를 2초 동안 채우는 코드
	    FadeinCanvas는 검은화면을 alpha값 조정으로 Fadein을 구현한 코드이며
	    포트폴리오에는 첨부하지 않았습니다.*/
            StartCoroutine(FillSlider(2));
            yield return new WaitUntil(() => loadingthings.LoadingSldier.value == 1f);
            yield return fade_inout.StartCoroutine(fade_inout.FadeinCanvas(10));
            sceneAsync.allowSceneActivation = true; 
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        /*유저정보가 없는 경우 닉네임을 정하기 위한 게임오브젝트 활성화, 노래 재생*/
        else
        {
            StartCoroutine(FillSlider(2));
            yield return new WaitUntil(() => loadingthings.LoadingSldier.value == 1f);
            yield return fade_inout.StartCoroutine(fade_inout.FadeinCanvas(10));
            loadingthings.panel_loding.SetActive(false);
            loadingthings.panel_Nick.SetActive(true);
            yield return fade_inout.StartCoroutine(fade_inout.FadeoutCanvas(1));
            /*팝업이 발생한 후 inputfield에 바로 텍스트를 적을 수 있게 하기 위함*/
            EventSystem.current.SetSelectedGameObject(loadingthings.Nickname_Field.gameObject);
            
            BackgroundManager audioManager = GameObject.Find("SoundObject").GetComponent<BackgroundManager>();
            if(audioManager != null)
            {
                if(!audioManager.IsMusicPlaying())
                {
                    audioManager.PlayMusic_A();
                }
            }
        }
    }
    
    /*Lobby씬 이동 시 노래 재생 및 Initialize 실행, 귓속말 시스템을 위해 Nickname을 서버에 저장*/
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if(scene.name == "Lobby") {
            lobbyclient.audioManager = GameObject.Find("SoundObject").GetComponent<BackgroundManager>();
            lobbyclient.Initialize();
            CmdServerSaveNick(ClientDataManager.Instance.UserDetails.Nickname, SteamInfo.SteamID);
            fade_inout.StartCoroutine(fade_inout.FadeoutCanvas(10));
        }
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    /*기존 접속확인에서 기존접속을 끊고 접속할 경우*/
    private void OnLoadfail1Clicked()
    {
        loadingthings.Load_fail.SetActive(false);
        clientAuth.QuitExist(SteamInfo.SteamID);
        StartCoroutine(CheckExistDone());
    }
    
    /*기존 접속이 끊어졌을 경우 서버로 부터 해당 정보를 받아
     clientAuth.DisM이 true로 변경*/
    private IEnumerator CheckExistDone()
    {
        float timeout = 20f;
        while (!clientAuth.DisM && timeout > 0)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }
        clientAuth.SendLoginMsg(SteamInfo.SteamID, "");
        clientAuth.DisM = false;
        StartCoroutine(CheckExist());
    }
    
    /*비속어 목록을 불러오기*/
    private void LoadProfanities()
    {
        string[] profanities = Resources.Load<TextAsset>("profanities").text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        profanitiesList.AddRange(profanities);
    }
    
    /*버튼 클릭 시 닉네임이 조건에 부합한지 클라이언트 차원에서 확인 후 서버로 전송*/
    private void OnNicknameSubmitClicked()
    {
        string nickname = loadingthings.Nickname_Field.text;
        SteamInfo.Nickname = loadingthings.Nickname_Field.text;
        
        if (!IsValidNickname(nickname))
        {
            loadingthings.Nick_Logfail2.SetActive(true);
            EventSystem.current.SetSelectedGameObject(loadingthings.Nick_Logfail2_btn.gameObject);
            loadingthings.Nickname_Field.text = "";
            if(UisoundManager != null)
                UisoundManager.PlayWarringSound();
            return;
        }
        NetworkIdentity opponentIdentity = GetComponent<NetworkIdentity>();
        CmdSubmitNickname(nickname, opponentIdentity);
    }
    
    /*닉네임의 조건*/
    private bool IsValidNickname(string nickname)
    {
        if (string.IsNullOrEmpty(nickname) || nickname.Length < 2 || nickname.Length > 12)
        {
            return false;
        }
        foreach (var profanity in profanitiesList)
        {
            if (nickname.Contains(profanity, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        Regex regex = new Regex(@"^[0-9a-zA-Z가-힣]{2,12}$");
        return regex.IsMatch(nickname);
    }
}
