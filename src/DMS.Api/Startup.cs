using Autofac;
using DMS.Api.Filter;
using DMS.Authorizations.Model;
using DMS.Authorizations.ServiceExtensions;
using DMS.Common.Extensions;
using DMS.Common.Helper;
using DMS.Common.JsonHandler.JsonConverters;
using DMS.Common.Model.Result;
using DMS.Extensions.ServiceExtensions;
using DMS.NLogs.Filters;
using DMS.Redis.Configurations;
using DMS.Services.RedisEvBus;
using DMS.Swagger;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DMS.Api
{
    /// <summary>
    /// 
    /// </summary>
    public class Startup
    {
        /// <summary>
        /// 
        /// </summary>
        public IConfiguration Configuration { get; }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="env"></param>
        public Startup(IWebHostEnvironment env)
        {
            var path = env.ContentRootPath;
            var builder = new ConfigurationBuilder()
            .SetBasePath(env.ContentRootPath)
            .AddJsonFile($"Configs/redis.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"Configs/domain.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.json", optional: true, reloadOnChange: true)
            .AddAppSettingsFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true);
            Configuration = builder.Build();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="services"></param>
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers(option =>
            {
                //ȫ�ִ����쳣��֧��DMS.Log4net��DMS.NLogs
                option.Filters.Add<GlobalExceptionFilter>();

            }).AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new DateTimeJsonConverter());
                //options.JsonSerializerOptions.PropertyNamingPolicy = null;
                //options.JsonSerializerOptions.DictionaryKeyPolicy = null;
            }).ConfigureApiBehaviorOptions(options =>
            {
                //ʹ���Զ���ģ����֤
                options.InvalidModelStateResponseFactory = (context) =>
                {
                    var result = new ResponseResult()
                    {
                        errno = 1,
                        errmsg = string.Join(Environment.NewLine, context.ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)))
                    };
                    return new JsonResult(result);
                };
            });
            #region ģ����֤���ַ�����ѡ��һ�ּ���
            //��һ�֣�ģ��ȫ����֤
            //option.Filters.Add<ApiResultFilterAttribute>();
            //.ConfigureApiBehaviorOptions(options =>
            // {
            //     options.SuppressModelStateInvalidFilter = true;//�ر���֤��param jsonת��Ϊ�ղ�����
            // });


            //�ڶ��֣�ģ��ȫ����֤
            //.ConfigureApiBehaviorOptions(options =>
            // {
            //     //ʹ���Զ���ģ����֤
            //     options.InvalidModelStateResponseFactory = (context) =>
            //     {
            //         var result = new ResponseResult();
            //         result.errmsg = string.Join(Environment.NewLine, context.ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)));
            //         return new JsonResult(result);
            //     };
            // });


            //�����֣�ģ��ȫ����֤
            //services.Configure<ApiBehaviorOptions>(options =>
            //{
            //    options.InvalidModelStateResponseFactory = (context) =>
            //    {
            //        var result = new ResponseResult();
            //        result.errmsg = string.Join(Environment.NewLine, context.ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)));
            //        return new JsonResult(result);
            //    };
            //});
            #endregion


            //api�ĵ����ɣ�1֧����ͨtoken��֤��2֧��oauth2�л���Ĭ��Ϊ1
            services.AddSwaggerGenSetup(option =>
            {
                option.RootPath = AppContext.BaseDirectory;
                option.XmlFiles = new List<string> {
                     AppDomain.CurrentDomain.FriendlyName+".xml",
                     "DMS.IServices.xml"
                };
            });
            //����HttpContext����
            services.AddHttpContextSetup();
            //����sqlsugar����
            services.AddSqlsugarIocSetup(Configuration);
            //����redis����
            services.AddRedisSetup();
            //����redismq����
            services.AddRedisMqSetup();
            //���������֤������api�ĵ���֤��Ӧ���ɣ�Ҫ�ȿ���redis����
            services.AddUserContextSetup();

            Permissions.IsUseIds4 = DMS.Common.AppConfig.GetValue(new string[] { "IdentityServer4", "Enabled" }).ToBool();
            services.AddAuthorizationSetup();
            // ��Ȩ+��֤ (jwt or ids4)
            if (Permissions.IsUseIds4)
            {
                services.AddAuthenticationIds4Setup();
            }
            else
            {
                services.AddAuthenticationJWTSetup();
            }
            //�����������
            //services.AddCorsSetup();
            services.Replace(ServiceDescriptor.Transient<IControllerActivator, ServiceBasedControllerActivator>());

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="app"></param>
        /// <param name="env"></param>
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            app.UseSwaggerUI(DebugHelper.IsDebug);
            // CORS����
            app.UseCors(DMS.Common.AppConfig.GetValue(new string[] { "Cors", "PolicyName" }));
            //������̬ҳ��
            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                  name: "default",
                  pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }

        /// <summary>
        /// �ӿ�ע��
        /// </summary>
        /// <param name="builder"></param>
        public void ConfigureContainer(ContainerBuilder builder)
        {
            builder.RegisterModule(new AutofacModuleRegister(AppContext.BaseDirectory, new List<string>()
            {
                "DMS.Services.dll",
            }));
            builder.RegisterModule<AutofacPropertityModuleRegister>();
        }

    }
}
