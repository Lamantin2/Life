using UnityEngine;

public class CellAnimator : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private float animationTime = 0.3f;
    private float currentAnimationTime = 0f;
    private bool isAnimating = false;
    private AnimationType currentAnimation;
    private Color targetColor;
    private Vector3 targetScale;
    private Vector3 originalScale;

    public enum AnimationType { Birth, Death, Pulse }

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalScale = transform.localScale;
    }

    void Update()
    {
        if (!isAnimating) return;

        currentAnimationTime += Time.deltaTime;
        float progress = Mathf.Clamp01(currentAnimationTime / animationTime);

        switch (currentAnimation)
        {
            case AnimationType.Birth:
                AnimateBirth(progress);
                break;
            case AnimationType.Death:
                AnimateDeath(progress);
                break;
            case AnimationType.Pulse:
                AnimatePulse(progress);
                break;
        }

        if (progress >= 1f)
        {
            isAnimating = false;
            // В случае смерти, возвращаем к нормальному размеру
            transform.localScale = originalScale;
        }
    }

    private void AnimateBirth(float progress)
    {
        // Анимация появления: от маленького к нормальному размеру
        float scale = Mathf.Lerp(0.1f, 1f, progress);
        transform.localScale = originalScale * scale;
        
        // Легкое свечение
        Color currentColor = spriteRenderer.color;
        currentColor.a = progress;
        spriteRenderer.color = currentColor;
    }

    private void AnimateDeath(float progress)
    {
        // Анимация исчезновения: уменьшение и прозрачность
        float scale = Mathf.Lerp(1f, 1.3f, progress); // Сначала немного увеличивается
        transform.localScale = originalScale * scale;
        
        Color currentColor = spriteRenderer.color;
        currentColor.a = 1f - progress;
        spriteRenderer.color = currentColor;
    }

    private void AnimatePulse(float progress)
    {
        // Пульсация для выделения
        float pulse = Mathf.Sin(progress * Mathf.PI * 2) * 0.2f + 1f;
        transform.localScale = originalScale * pulse;
    }

    public void PlayAnimation(AnimationType type, Color color)
    {
        currentAnimation = type;
        currentAnimationTime = 0f;
        isAnimating = true;
        targetColor = color;
        spriteRenderer.color = color;

        switch (type)
        {
            case AnimationType.Birth:
                transform.localScale = originalScale * 0.1f;
                break;
            case AnimationType.Death:
                transform.localScale = originalScale;
                break;
        }
    }

    public void StopAnimation()
    {
        isAnimating = false;
        transform.localScale = originalScale;
    }
}