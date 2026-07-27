using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace AeroTerra.UI
{
    /// <summary>
    /// Full-bleed background photo(s) for a menu screen. One Resources path shows a
    /// single static image (Settings); two or more slowly crossfade on a loop (Main
    /// Menu's slider). Two stacked RawImage layers ping-pong the crossfade instead of
    /// keeping every image alive at once. Missing/unresolved paths are skipped rather
    /// than rendering a blank white quad, so the parent's own background color still
    /// shows through if a photo fails to load.
    /// </summary>
    public class BackgroundSlider : MonoBehaviour
    {
        private Texture2D[] _textures;
        private RawImage _a, _b;
        private float _intervalSec, _fadeSec;
        private int _index;

        public void Init(Transform parent, string[] resourcePaths, float intervalSec = 7f, float fadeSec = 1.2f)
        {
            _intervalSec = intervalSec;
            _fadeSec = fadeSec;
            _textures = resourcePaths.Select(p => Resources.Load<Texture2D>(p)).Where(t => t != null).ToArray();
            if (_textures.Length == 0) return;

            _a = BuildLayer(parent, "BgSliderA");
            _b = BuildLayer(parent, "BgSliderB");

            _a.texture = _textures[0];
            SetAlpha(_a, 1f);

            if (_textures.Length > 1) StartCoroutine(CycleRoutine());
        }

        private static RawImage BuildLayer(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RawImage));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<RawImage>();
            img.color = new Color(1f, 1f, 1f, 0f);
            return img;
        }

        private static void SetAlpha(RawImage img, float a)
        {
            var c = img.color;
            c.a = a;
            img.color = c;
        }

        private IEnumerator CycleRoutine()
        {
            var front = _a;
            var back = _b;
            while (true)
            {
                yield return new WaitForSecondsRealtime(_intervalSec);

                _index = (_index + 1) % _textures.Length;
                back.texture = _textures[_index];
                SetAlpha(back, 0f);

                float t = 0f;
                while (t < _fadeSec)
                {
                    t += Time.unscaledDeltaTime;
                    float k = Mathf.Clamp01(t / _fadeSec);
                    SetAlpha(back, k);
                    SetAlpha(front, 1f - k);
                    yield return null;
                }
                SetAlpha(back, 1f);
                SetAlpha(front, 0f);

                (front, back) = (back, front);
            }
        }
    }
}
