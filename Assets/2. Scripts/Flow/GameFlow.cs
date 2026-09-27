using UnityEngine;
using UnityEngine.SceneManagement;

namespace GoDeep
{
    public sealed class GameFlow : MonoBehaviour
    {
        [SerializeField] private CanvasGroup menuGroup; //씬 이동 중 입력을 잠글 메뉴
        private bool loading; //중복 씬 이동 방지 상태

        public void openStandBy() //준비 화면으로 이동
        {
            loadScene("StandBy");
        }

        public void startPrototype() //로컬 조작 시제품 시작
        {
            loadScene("Play");
        }

        public void returnHome() //홈 화면으로 이동
        {
            loadScene("Home");
        }

        public void restartPrototype() //현재 시제품 상태를 처음부터 재시작
        {
            loadScene("Play");
        }

        private void loadScene(string sceneName) //빌드 목록의 씬으로 비동기 이동
        {
            if (loading) return;
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError("Scene is not in Build Settings: " + sceneName, this);
                return;
            }
            loading = true;
            if (menuGroup != null) menuGroup.interactable = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SceneManager.LoadSceneAsync(sceneName);
        }
    }
}
