using Microsoft.Extensions.DependencyInjection;
using Team.Repositories.Posts;

namespace Team.Services.Posts;

public static class PostsModule
{
    /// <summary>Module Posts (BE-31): bài tuyển thành viên của nhóm, bài "Tôi đang tìm nhóm" của sinh viên. Cần Teams.</summary>
    public static IServiceCollection AddPostsModule(this IServiceCollection services)
    {
        services.AddScoped<IPostRepository, PostRepository>();
        services.AddScoped<IPostService, PostService>();
        return services;
    }
}
