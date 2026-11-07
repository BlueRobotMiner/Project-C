// Copyright (c) 2026 Rasterloom. All rights reserved.
using UnityEngine;

namespace Rasterloom.FreeIconSampler
{
    /// <summary>
    /// Walks the camera down the icon wall so the demo scene shows something
    /// the moment you press Play, then turns around at the ends.
    /// </summary>
    [DisallowMultipleComponent]
    public class IconTourCamera : MonoBehaviour
    {
        [Tooltip("World units per second.")]
        public float speed = 2.5f;

        [Tooltip("How far down the wall the tour runs before turning back.")]
        public float travel = 8f;

        [Tooltip("Show the two store buttons in the corner of the Game view.")]
        public bool showStoreButtons = true;

        private const string FullSetUrl =
            "https://assetstore.unity.com/packages/2d/gui/icons/500-game-ui-icons-svg-png-sprite-sheet-405680";
        private const string ReviewUrl =
            "https://assetstore.unity.com/packages/2d/gui/icons/60-free-game-ui-icons-svg-png-tintable-sprites-407082#reviews";

        private Vector3 _origin;
        private float _t;

        private void Awake()
        {
            _origin = transform.position;
        }

        private void Update()
        {
            // PingPong rather than a wrap: a hard jump back to the top reads
            // as a dropped frame, and reviewers do notice.
            _t += Time.deltaTime * speed;
            transform.position = _origin + Vector3.down * Mathf.PingPong(_t, travel);
        }

        private void OnGUI()
        {
            if (!showStoreButtons)
            {
                return;
            }

            // Two small buttons in the bottom-left corner of the demo only.
            // Nothing here runs in your own scenes unless you add this
            // component to a camera there, and the toggle above hides it.
            const float w = 210f, h = 26f, pad = 10f;
            float y = Screen.height - h - pad;
            if (GUI.Button(new Rect(pad, y, w, h), "All 520 icons (Asset Store)"))
            {
                Application.OpenURL(FullSetUrl);
            }
            if (GUI.Button(new Rect(pad * 2f + w, y, 150f, h), "Rate this free pack"))
            {
                Application.OpenURL(ReviewUrl);
            }
        }
    }
}
