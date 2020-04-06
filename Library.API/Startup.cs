﻿using AutoMapper;
using Library.API.Contexts;
using Library.API.OperationFilters;
using Library.API.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

[assembly: ApiConventionType(typeof(DefaultApiConventions))]
namespace Library.API
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc(setupAction =>
            {
                //// add "status code" to all controllers
                setupAction.Filters.Add(new ProducesResponseTypeAttribute(StatusCodes.Status400BadRequest));
                setupAction.Filters.Add(new ProducesResponseTypeAttribute(StatusCodes.Status406NotAcceptable));
                setupAction.Filters.Add(new ProducesResponseTypeAttribute(StatusCodes.Status500InternalServerError));

                // for status code 406
                setupAction.ReturnHttpNotAcceptable = true;

                setupAction.OutputFormatters.Add(new XmlSerializerOutputFormatter());

                var jsonOutputFormatter = setupAction.OutputFormatters
                    .OfType<JsonOutputFormatter>().FirstOrDefault();

                if (jsonOutputFormatter != null)
                {
                    // remove text/json as it isn't the approved media type
                    // for working with JSON at API level
                    if (jsonOutputFormatter.SupportedMediaTypes.Contains("text/json"))
                    {
                        jsonOutputFormatter.SupportedMediaTypes.Remove("text/json");
                    }
                }
            })
            .SetCompatibilityVersion(CompatibilityVersion.Version_2_2);

            // register the DbContext on the container, getting the connection string from
            // appSettings (note: use this during development; in a production environment,
            // it's better to store the connection string in an environment variable)
            var connectionString = Configuration["ConnectionStrings:LibraryDBConnectionString"];
            services.AddDbContext<LibraryContext>(o => o.UseSqlServer(connectionString));
            
            services.Configure<ApiBehaviorOptions>(options =>
            {
                options.InvalidModelStateResponseFactory = actionContext =>
                {
                    var actionExecutingContext =
                        actionContext as Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext;

                    // if there are modelstate errors & all keys were correctly
                    // found/parsed we're dealing with validation errors
                    if (actionContext.ModelState.ErrorCount > 0
                        && actionExecutingContext?.ActionArguments.Count == actionContext.ActionDescriptor.Parameters.Count)
                    {
                        return new UnprocessableEntityObjectResult(actionContext.ModelState);
                    }

                    // if one of the keys wasn't correctly found / couldn't be parsed
                    // we're dealing with null/unparsable input
                    return new BadRequestObjectResult(actionContext.ModelState);
                };
            });

            services.AddScoped<IBookRepository, BookRepository>();
            services.AddScoped<IAuthorRepository, AuthorRepository>();

            services.AddAutoMapper();

            services.AddVersionedApiExplorer(setupAction =>
            {
                setupAction.GroupNameFormat = "'v'VV";
            });

            services.AddApiVersioning(setupAction =>
            {
                setupAction.AssumeDefaultVersionWhenUnspecified = true;
                setupAction.DefaultApiVersion = new ApiVersion(1, 0);
                setupAction.ReportApiVersions = true;
                //setupAction.ApiVersionReader = new HeaderApiVersionReader("api-version");
                //setupAction.ApiVersionReader = new MediaTypeApiVersionReader();
            });

            var apiVersionDescriptionProvider = services.BuildServiceProvider().GetService<IApiVersionDescriptionProvider>();

            services.AddSwaggerGen(setupAction =>
            {
                foreach(var description in apiVersionDescriptionProvider.ApiVersionDescriptions)
                {
                    setupAction.SwaggerDoc(
                    $"LibraryOpenAPISpecification{description.GroupName}",
                    new Microsoft.OpenApi.Models.OpenApiInfo()
                    {
                        Title = "Library API",
                        Version = description.ApiVersion.ToString(),
                        Description = "Through this API you can access authors and books",
                        Contact = new Microsoft.OpenApi.Models.OpenApiContact()
                        {
                            Email = "oscaar_2003@hotmail.com",
                            Name = "Oscar C. Loma",
                            Url = new Uri("https://github.com/0scaar/DocumentingWithSwagger")
                        },
                        License = new Microsoft.OpenApi.Models.OpenApiLicense()
                        {
                            Name = "MIT License",
                            Url = new Uri("https://opensource.org/licenses/MIT")
                        }
                    });
                }



                //setupAction.SwaggerDoc(
                //    "LibraryOpenAPISpecificationAuthors",
                //    new Microsoft.OpenApi.Models.OpenApiInfo()
                //    {
                //        Title = "Library API (Authors)",
                //        Version = "1",
                //        Description = "Through this API you can access authors",
                //        Contact = new Microsoft.OpenApi.Models.OpenApiContact()
                //        {
                //            Email = "oscaar_2003@hotmail.com",
                //            Name = "Oscar C. Loma",
                //            Url = new Uri("https://github.com/0scaar/DocumentingWithSwagger")
                //        },
                //        License = new Microsoft.OpenApi.Models.OpenApiLicense()
                //        {
                //            Name = "MIT License",
                //            Url = new Uri("https://opensource.org/licenses/MIT")
                //        }
                //    });

                //setupAction.SwaggerDoc(
                //    "LibraryOpenAPISpecificationBooks",
                //    new Microsoft.OpenApi.Models.OpenApiInfo()
                //    {
                //        Title = "Library API (Books)",
                //        Version = "1",
                //        Description = "Through this API you can access books",
                //        Contact = new Microsoft.OpenApi.Models.OpenApiContact()
                //        {
                //            Email = "oscaar_2003@hotmail.com",
                //            Name = "Oscar C. Loma",
                //            Url = new Uri("https://github.com/0scaar/DocumentingWithSwagger")
                //        },
                //        License = new Microsoft.OpenApi.Models.OpenApiLicense()
                //        {
                //            Name = "MIT License",
                //            Url = new Uri("https://opensource.org/licenses/MIT")
                //        }
                //    });

                setupAction.DocInclusionPredicate((documentName, apiDescription) =>
                {
                    var actionApiVerionModel = apiDescription.ActionDescriptor
                        .GetApiVersionModel(ApiVersionMapping.Explicit | ApiVersionMapping.Implicit);

                    if (actionApiVerionModel == null)
                        return true;

                    if (actionApiVerionModel.DeclaredApiVersions.Any())
                    {
                        return actionApiVerionModel.DeclaredApiVersions.Any(v =>
                            $"LibraryOpenAPISpecificationv{v.ToString()}" == documentName);
                    }

                    return actionApiVerionModel.ImplementedApiVersions.Any(v =>
                        $"LibraryOpenAPISpecificationv{v.ToString()}" == documentName);
                });

                setupAction.OperationFilter<GetBookOperationFilter>();
                setupAction.OperationFilter<CreateBookOperationFilter>();

                var xmlComentsFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlComentsFullPath = Path.Combine(AppContext.BaseDirectory, xmlComentsFile);

                setupAction.IncludeXmlComments(xmlComentsFullPath);
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, 
            IApiVersionDescriptionProvider apiVersionDescriptionProvider)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                // The default HSTS value is 30 days. 
                // You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseSwagger();

            app.UseSwaggerUI(setupAction =>
            {
                foreach(var description in apiVersionDescriptionProvider.ApiVersionDescriptions)
                {
                    setupAction.SwaggerEndpoint(
                    $"/swagger/LibraryOpenAPISpecification{description.GroupName}/swagger.json",
                    description.GroupName.ToUpperInvariant());
                }

                //setupAction.SwaggerEndpoint(
                //    "/swagger/LibraryOpenAPISpecification/swagger.json",
                //    "Library API");

                //setupAction.SwaggerEndpoint(
                //    "/swagger/LibraryOpenAPISpecificationAuthors/swagger.json",
                //    "Library API (Authors)");
                //setupAction.SwaggerEndpoint(
                //    "/swagger/LibraryOpenAPISpecificationBooks/swagger.json",
                //    "Library API (Books)");
                setupAction.RoutePrefix = "";
            });

            app.UseStaticFiles();

            app.UseMvc();
        }
    }
}
