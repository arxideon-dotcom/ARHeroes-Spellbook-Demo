// ARHeroes-Spellbook-Demo — hackathon build (Oct 2026).
// Cycles transparent PNG frames of a spell-page animation.
// Attach to a GameObject with a SpriteRenderer (the right page of the book).
using UnityEngine;
public class LoopPlayer : MonoBehaviour
{
    [Header("Frames (transparent PNG sequence)")]
    public Sprite[] frames;
    [Tooltip("Playback speed in frames per second.")]
    public float fps = 12f;
    public bool loop = true;
    public bool playOnStart = true;
    private SpriteRenderer _renderer;
    private float _timer;
    private int _index;
    private bool _playing;
    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        if (_renderer == null)
            Debug.LogError("LoopPlayer needs a SpriteRenderer on the same GameObject.", this);
    }
    private void Start()
    {
        if (playOnStart) Play();
        else ShowFrame(0);
    }
    private void Update()
    {
        if (!_playing || frames == null || frames.Length == 0 || fps <= 0f) return;
        _timer += Time.deltaTime;
        float frameTime = 1f / fps;
        while (_timer >= frameTime)
        {
            _timer -= frameTime;
            _index++;
            if (_index >= frames.Length)
            {
                if (loop) _index = 0;
                else { _index = frames.Length - 1; _playing = false; break; }
            }
        }
        ShowFrame(_index);
    }
    private void ShowFrame(int i)
    {
        if (_renderer != null && frames != null && frames.Length > 0)
            _renderer.sprite = frames[Mathf.Clamp(i, 0, frames.Length - 1)];
    }
    public void Play() { _index = 0; _timer = 0f; _playing = true; ShowFrame(0); }
    public void Pause() { _playing = false; }
    public void Resume() { if (frames != null && frames.Length > 0) _playing = true; }
    public void Replay() { Play(); }
}
