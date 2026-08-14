using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Turbo.Admin.Configuration;
using Turbo.Database.Entities.Admin;

namespace Turbo.Admin.Auth;

public sealed class JwtTokenService(IOptions<AdminConfig> config) : IJwtTokenService
{
    private readonly AdminConfig _config = config.Value;

    public string IssueToken(AdminUserEntity adminUser)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config.Jwt.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, adminUser.Id.ToString()),
            new(ClaimTypes.Name, adminUser.Username),
            new(ClaimTypes.Role, adminUser.Role.ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: _config.Jwt.Issuer,
            audience: _config.Jwt.Issuer,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_config.Jwt.ExpiryMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
