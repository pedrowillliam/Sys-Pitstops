namespace SysPitstops.Api.Auth;

public static class AuthCookie
{
    public const string Name = "sys_pitstops_token";

    public static CookieOptions Build(IWebHostEnvironment env, DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        Secure = !env.IsDevelopment(),
        Path = "/",
        Expires = expires
    };
}
