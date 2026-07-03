//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gridr.Chess
{
    public class Restarter : MonoBehaviour
    {
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                OnRestart();
            }
        }

        private void OnRestart()
        {
            SceneManager.LoadScene("CHESS_sample", LoadSceneMode.Single);
        }

    }
}