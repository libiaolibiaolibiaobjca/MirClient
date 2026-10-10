namespace MirClient.Runner.Scenes;

/// <summary>
/// 客户端视口场景接口
/// </summary>
public interface IScene : IDisposable
{
    void Init();
    void Update(float dt);
    void Render();
    void OnResize(int width, int height);
}
