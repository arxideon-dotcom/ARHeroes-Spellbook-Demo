// ARHeroes-Spellbook-Demo — hackathon build (Oct 2026).
// Показывает текущий кадр LoopPlayer на изогнутой поверхности страницы. LoopPlayer по-прежнему
// крутит кадры в своём SpriteRenderer (тайминг, цикл, Play/Replay не меняются), а этот UI Image
// с PageWarp каждый кадр берёт тот же спрайт и рисует его по листу.
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class SpriteMirror : MonoBehaviour
{
    public SpriteRenderer source;
    private Image _image;

    private void LateUpdate()
    {
        if (_image == null) _image = GetComponent<Image>();
        if (source != null && _image.sprite != source.sprite) _image.sprite = source.sprite;
    }
}
