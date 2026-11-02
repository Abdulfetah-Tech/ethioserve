using EthioServe.Application.Auth;
using EthioServe.Application.Catalog;
using EthioServe.Application.Common;
using EthioServe.Application.Favorites;
using EthioServe.Application.Locations;
using EthioServe.Application.Messages;
using EthioServe.Application.Payments;
using EthioServe.Application.ProviderDocuments;
using EthioServe.Application.Providers;
using EthioServe.Application.Appointments;
using EthioServe.Application.Attachments;
using EthioServe.Application.Notifications;
using EthioServe.Application.Quotes;
using EthioServe.Application.Requests;
using EthioServe.Application.Reviews;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace EthioServe.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<RegisterCustomerRequestValidator>();

        services.AddScoped<ILocationQueries, LocationQueries>();
        services.AddScoped<ICatalogQueries, CatalogQueries>();
        services.AddScoped<IAdminCatalogService, AdminCatalogService>();
        services.AddScoped<IProviderSelfService, ProviderSelfService>();
        services.AddScoped<IAdminProviderService, AdminProviderService>();
        services.AddScoped<IProviderSearchQueries, ProviderSearchQueries>();
        services.AddScoped<ICustomerRequestService, CustomerRequestService>();
        services.AddScoped<IProviderFeedService, ProviderFeedService>();
        services.AddScoped<IRequestAttachmentService, RequestAttachmentService>();
        services.AddScoped<IProviderQuoteService, ProviderQuoteService>();
        services.AddScoped<ICustomerQuoteService, CustomerQuoteService>();
        services.AddScoped<IProviderAppointmentService, ProviderAppointmentService>();
        services.AddScoped<ICustomerAppointmentService, CustomerAppointmentService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IMessageService, MessageService>();
        services.AddScoped<IFavoriteService, FavoriteService>();
        services.AddScoped<ICustomerPaymentService, CustomerPaymentService>();
        services.AddScoped<IProviderPaymentService, ProviderPaymentService>();
        services.AddScoped<IProviderDocumentService, ProviderDocumentService>();
        services.AddScoped<INotifier, Notifier>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IAuditTrail, AuditTrail>();
        return services;
    }
}
