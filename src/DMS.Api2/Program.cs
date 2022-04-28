using DMS.Api2.Authorizations.Model;
using DMS.Api2.Authorizations.Policys;
using DMS.Common.Extensions;
using DMS.Common.JsonHandler.JsonConverters;
using DMS.Extensions.ServiceExtensions;
using DMS.Swagger;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Host
.ConfigureAppConfiguration((hostingContext, config) =>
{
    config.Sources.Clear();
    config.AddAppSettingsFile($"appsettings.json", optional: true, reloadOnChange: true);
});

builder.Services.AddControllers()
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new DateTimeJsonConverter());
    //options.JsonSerializerOptions.PropertyNamingPolicy = null;
    //options.JsonSerializerOptions.DictionaryKeyPolicy = null;
});
//api�ĵ����ɣ�1֧����ͨtoken��֤��2֧��oauth2�л���Ĭ��Ϊ1
builder.Services.AddSwaggerGenSetup(option =>
{
    option.RootPath = AppContext.BaseDirectory;
    option.XmlFiles = new List<string> {
        AppDomain.CurrentDomain.FriendlyName+".xml"
    };
});
//����HttpContext����
builder.Services.AddHttpContextSetup();




builder.Services.Configure<JwtSettingModel>(builder.Configuration.GetSection("JwtSetting"));
JwtSettingModel config = new JwtSettingModel();
builder.Configuration.GetSection("JwtSetting").Bind(config);

string issuer = config.Issuer;
string audience = config.Audience;
string secretCredentials = config.SecretKey;
double expireMinutes = config.ExpireMinutes;

// ������֤����
var tokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuer = true,//�Ƿ���֤������
    ValidIssuer = issuer,//������

    ValidateAudience = true,//�Ƿ���֤��������
    ValidAudience = audience,//������
                             //������ö�̬��֤�ķ�ʽ�������µ�½ʱ��ˢ��token����token��ǿ��ʧЧ��
                             //AudienceValidator = (m, n, z) =>
                             //{
                             //    return m != null && m.FirstOrDefault().Equals(audience);
                             //},

    ValidateIssuerSigningKey = true,//�Ƿ���֤��Կ
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(secretCredentials)),

    ValidateLifetime = true, //��֤��������
    ClockSkew = TimeSpan.FromSeconds(30),//ע�����ǻ������ʱ�䣬�ܵ���Чʱ��������ʱ�����jwt�Ĺ���ʱ�䣬��������ã�Ĭ����5����
    RequireExpirationTime = true, //����ʱ��
};
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Permission",
       policy => policy.Requirements.Add(new PolicyRequirement()));
});
builder.Services.AddSingleton<IAuthorizationHandler, PolicyHandler>();

builder.Services.AddAuthentication(x =>
{
    //x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    //x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    //x.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;


    x.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = nameof(ApiResponseHandler);
    x.DefaultForbidScheme = nameof(ApiResponseHandler);

})
.AddJwtBearer(o =>
{
    //��ʹ��https
    //o.RequireHttpsMetadata = false;
    o.TokenValidationParameters = tokenValidationParameters;
    o.Events = new JwtBearerEvents
    {
        OnChallenge = context =>
        {
            context.Response.Headers.Add("Token-Error", context.ErrorDescription);
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = context =>
        {
            var jwtHandler = new JwtSecurityTokenHandler();
            var token = context.Request.Headers["Authorization"].ToStringDefault().Replace("Bearer ", "");

            if (!token.IsNullOrEmpty() && jwtHandler.CanReadToken(token))
            {
                var jwtToken = jwtHandler.ReadJwtToken(token);

                if (jwtToken.Issuer != issuer)
                {
                    context.Response.Headers.Add("Token-Error-Iss", "issuer is wrong!");
                }

                if (jwtToken.Audiences.FirstOrDefault() != audience)
                {
                    context.Response.Headers.Add("Token-Error-Aud", "Audience is wrong!");
                }
            }


            // ������ڣ����<�Ƿ����>��ӵ�������ͷ��Ϣ��
            if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
            {
                context.Response.Headers.Add("Token-Expired", "true");
            }
            return Task.CompletedTask;
        }
    };
})
.AddScheme<AuthenticationSchemeOptions, ApiResponseHandler>(nameof(ApiResponseHandler), o => { });


var app = builder.Build();
app.UseSwaggerUI(true);


app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
