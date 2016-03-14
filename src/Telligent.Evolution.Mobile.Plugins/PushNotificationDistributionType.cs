using System;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using PushSharp.Core;
using Telligent.Evolution.Components;
using Telligent.Evolution.Extensibility.Version1;
using Telligent.Evolution.Extensibility.Content.Version1;
using Telligent.DynamicConfiguration.Components;
using Telligent.Evolution.Extensibility.Api.Version1;
using Telligent.Evolution.Extensibility.Api.Entities.Version1;
using Telligent.Evolution.Mobile.PushNotifications.Services;
using Telligent.Evolution.Mobile.PushNotifications.Model;
using Telligent.Evolution.Extensibility.Rest.Version2;
using PushSharp.Google;
using PushSharp.Apple;

namespace Telligent.Evolution.Mobile.PushNotifications
{
	public class PushNotificationDistributionType : IPlugin, IConfigurablePlugin, ITranslatablePlugin, INotificationDistributionType, IRestEndpoints, IInstallablePlugin, IRequiredConfigurationPlugin
	{
		readonly Guid _id = new Guid("d446b6502ca942fd9c496ec868839d68");
		ITranslatablePluginController _translation;
		IPluginConfiguration _configuration;
		IDeviceRegistrationService _deviceRegistrationService = ServiceLocator.Get<IDeviceRegistrationService>();
		IDeviceRegistrationDataService _deviceRegistrationDataService = ServiceLocator.Get<IDeviceRegistrationDataService>();
        ApnsServiceBroker _pushApple = null;
	    GcmServiceBroker _pushGoogle = null;

		DeviceType _enabledDevices = DeviceType.None;

		#region IPlugin Members

		public string Description
		{
			get { return "Implements support for push notifications for iOS and Android devices."; }
		}

		public void Initialize()
		{
			_enabledDevices = DeviceType.None;

			StringBuilder debug = new StringBuilder();

			if (_configuration != null)
			{
				if (!string.IsNullOrEmpty(_configuration.GetCustom("apnsCertificate"))
					&& !string.IsNullOrEmpty(_configuration.GetString("apnsPassword")))
				{
					var certificate = PropertyControls.CertificatePropertyControl.Deserialize(_configuration.GetCustom("apnsCertificate"));
					if (certificate != null)
					{
						try
						{
                            _pushApple = new ApnsServiceBroker(new ApnsConfiguration(_configuration.GetBool("apnsProduction") ? ApnsConfiguration.ApnsServerEnvironment.Production : ApnsConfiguration.ApnsServerEnvironment.Sandbox , certificate.Content, _configuration.GetString("apnsPassword")));

							_enabledDevices |= DeviceType.IOS;

                            _pushApple.OnNotificationFailed += OnNotificationFailed;

							debug.Append("iOS notifications are configured and enabled.\n");
						}
						catch (Exception ex)
						{
							debug.Append("An exception occurred while attempting to configure iOS notifications:\n").Append(ex.ToString());
						}
					}
					else
						debug.Append("The certificate for iOS notifications is invalid. iOS notifications are not enabled.\n");
				}
				else
				{
					debug.Append("Configuration for iOS notifications is incomplete. iOS notifications are not enabled.\n");
				}

				if (!string.IsNullOrEmpty(_configuration.GetString("gcmPackageName"))
					&& !string.IsNullOrEmpty(_configuration.GetString("gcmSenderId"))
					&& !string.IsNullOrEmpty(_configuration.GetString("gcmAuthorizationKey")))
				{
					try
					{
                        _pushGoogle = new GcmServiceBroker(new GcmConfiguration(_configuration.GetString("gcmSenderId"), _configuration.GetString("gcmAuthorizationKey"), _configuration.GetString("gcmPackageName")));

					    _pushGoogle.OnNotificationFailed += OnNotificationFailed;

                        _enabledDevices |= DeviceType.Android;

						debug.Append("Android notifications are configured and enabled.\n");
					}
					catch (Exception ex)
					{
						debug.Append("An exception occurred while attempting to enable Android notifications:\n").Append(ex.ToString());
					}
				}
				else
				{
					debug.Append("Configuration for Android notifications is incomplete. Android notifications are not enabled.\n");
				}

				if (_configuration.GetBool("enableLogging"))
					Telligent.Evolution.Extensibility.Api.Version1.PublicApi.Eventlogs.Write(debug.ToString(), new EventLogEntryWriteOptions { Category = "Push Notifications", EventId = 1234, EventType = "Information" });
			}			
		}

        private void OnNotificationFailed(INotification notification , string deviceToken, AggregateException exception)
        {
            if (notification.IsDeviceRegistrationIdValid())
            {
                new CSException(CSExceptionType.UnknownError, "An exception occurred while sending a apple push notification", exception).Log();
                
            }

            _deviceRegistrationService.UnregisterDevice(deviceToken);
        }
        
        private void OnNotificationFailed(GcmNotification notification, AggregateException exception)
        {
            OnNotificationFailed(notification, notification.CollapseKey, exception);
        }

	    private void OnNotificationFailed(ApnsNotification notification, AggregateException exception)
	    {
            OnNotificationFailed(notification, notification.DeviceToken, exception);
	    }

		public string Name
		{
			get { return "Mobile Push Notifications"; }
		}

		#endregion

		#region IConfigurablePlugin Members

		public PropertyGroup[] ConfigurationOptions
		{
			get 
			{
				PropertyGroup dataGroup = new PropertyGroup("configuration", "Data", 0);

				var connectionType = new Property("connectionType", "Connection Type", PropertyType.String, 0, "connectionStringName") { ControlType = typeof(Telligent.Evolution.Controls.PropertyVisibilityValueSelectionControl) };
				connectionType.SelectableValues.Add(new PropertyValue("connectionStringName", "Connection String Name", 1));
				connectionType.SelectableValues[0].Attributes["propertiesToHide"] = "connectionString";
				connectionType.SelectableValues[0].Attributes["propertiesToShow"] = "connectionStringName";
				connectionType.SelectableValues.Add(new PropertyValue("connectionString", "Connection String", 2));
				connectionType.SelectableValues[1].Attributes["propertiesToHide"] = "connectionStringName";
				connectionType.SelectableValues[1].Attributes["propertiesToShow"] = "connectionString";
				dataGroup.Properties.Add(connectionType);

				dataGroup.Properties.Add(new Property("connectionString", "Database Connection String", PropertyType.String, 1, ""));
				dataGroup.Properties.Add(new Property("connectionStringName", "Connection String Name", PropertyType.String, 2, "SiteSqlServer"));
				dataGroup.Properties.Add(new Property("enableLogging", "Enable Logging", PropertyType.Bool, 3, "False") { DescriptionText = "When enabled, debugging information will be logged to the Zimbra Community event log." });

				PropertyGroup iosGroup = new PropertyGroup("iosConfiguration", "iOS", 1);
				iosGroup.Properties.Add(new Property("apnsCertificate", "Apple Push Notification Service Certificate (.p12 file)", PropertyType.Custom, 0, "") { ControlType = typeof(Telligent.Evolution.Mobile.PushNotifications.PropertyControls.CertificatePropertyControl), DescriptionText = "This is a .p12 export of the push notification SSL certificate provided by Apple." });
				iosGroup.Properties.Add(new Property("apnsPassword", "Apple Push Notification Service Certificate Password", PropertyType.String, 1, "") { DescriptionText = "The password used to secure the .p12 certificate." });
				iosGroup.Properties.Add(new Property("apnsProduction", "Use Apple Push Notification Production Services", PropertyType.Bool, 2, true.ToString()) { DescriptionText = "Enable to use production services.  Otherwise, development services will be used." });

				PropertyGroup androidGroup = new PropertyGroup("androidConfiguration", "Android", 2);
				androidGroup.Properties.Add(new Property("gcmPackageName", "Android Application Package Name", PropertyType.String, 0, "com.telligent.evolution.mobile") { DescriptionText = "The name of the Android application notifications will be sent to." });
				androidGroup.Properties.Add(new Property("gcmSenderId", "Google Cloud Messaging Sender ID", PropertyType.String, 1, "") { DescriptionText = "The project ID of the Google API project through which notifications should be sent." });
				androidGroup.Properties.Add(new Property("gcmAuthorizationKey", "Google Cloud Messaging Authorization Key", PropertyType.String, 2, "") { DescriptionText = "The authorization key associated to the Google API project." });

				return new PropertyGroup[] { dataGroup, iosGroup, androidGroup };
			}
		}

		public void Update(IPluginConfiguration configuration)
		{
			_configuration = configuration;

			if (configuration.GetString("connectionType") == "connectionString")
				_deviceRegistrationDataService.ConnectionString = configuration.GetString("connectionString");
			else
				_deviceRegistrationDataService.ConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings[configuration.GetString("connectionStringName")].ConnectionString;
		}

		#endregion

		#region ITranslatablePlugin Members

		public Translation[] DefaultTranslations
		{
			get 
			{
				Translation translation = new Translation("en-US");
				translation.Set("distribution_type_name", "Mobile");
				translation.Set("distriubtion_type_description", "Push notifications to mobile devices");

				translation.Set("rest_invalid_device", "The device type was missing or invalid.");
				translation.Set("rest_invalid_token", "The device token was missing or invalid.");
				translation.Set("rest_user_invalid", "The accessing user was not found or represented a system account.");

				return new Translation[] { translation };
			}
		}

		public void SetController(ITranslatablePluginController controller)
		{
			_translation = controller;
		}

		#endregion

		#region INotificationDistributionType Members

		public bool Distribute(Notification notification, NotificationUserChanges userChanges)
		{
			bool distributed = false;

			if (_configuration == null)
				return false;

			var enableLogging = _configuration.GetBool("enableLogging");

			StringBuilder debug = new StringBuilder();
			try
			{
				if (userChanges != NotificationUserChanges.UsersAdded)
					return false;

				if (notification == null || notification.UserId == default(int))
					return false;

				var devices = _deviceRegistrationService.GetDevices(notification.UserId);
				if (devices.Any())
				{
					if (enableLogging)
						debug.Append("Notifying ").Append(notification.UserId).Append(" of '").Append(notification.Message("ShortText")).Append("'\n");

					var unreadCount = 0;
					var notifications = PublicApi.Notifications.List(new NotificationListOptions { UserId = notification.UserId, IsRead = false, PageIndex = 0, PageSize = 1 });
					if (notifications != null && notifications.Errors.Count == 0)
						unreadCount = notifications.TotalCount;

					if (enableLogging)
						debug.Append("Unread count: ").Append(unreadCount).Append("\n");

					var notificationId = notification.NotificationId.ToString("N");
					var message = PublicApi.Html.Decode(notification.Message("ShortText"));
					if (message.Length > 180)
						message = message.Substring(0, 177) + "...";

					if (enableLogging)
						debug.Append("Message: ").Append(PublicApi.Html.Encode(message)).Append("\n");

					foreach (var device in devices)
					{
						if (enableLogging)
						{
							debug.Append("Attempting to send to device: ").Append(device.Type.ToString()).Append("...\n");
							debug.Append("Device type enabled :").Append((device.Type & _enabledDevices) == device.Type).Append("\n");
						}

						if ((device.Type & _enabledDevices) == DeviceType.Android)
						{
							if (enableLogging)
								debug.Append("Sending to Google...\n");

                            _pushGoogle.QueueNotification(new GcmNotification()
                            {
                                CollapseKey = device.Token,
                                ContentAvailable = true,
                                Data = JObject.Parse(string.Concat(
                                        "{\"message\":\"",
                                        System.Web.HttpUtility.JavaScriptStringEncode(message, false),
                                        "\",\"msgcnt\":",
                                        unreadCount.ToString("#####"),
                                        ",\"n\":\"",
                                        notificationId,
                                        "\"}"))
                            });

							distributed = true;
						}
						else if ((device.Type & _enabledDevices) == DeviceType.IOS)
						{
							if (enableLogging)
								debug.Append("Sending to Apple...\n");

                            _pushApple.QueueNotification(new ApnsNotification(device.Token)
                            {
                                Payload = JObject.Parse (string.Format("{{ \"aps\" : " +
                                                                       "{{ \"alert\" : \"{0}\" , \"sound\" : \"default\" , \"badge\":\"{1}\"}} " +
                                                                       string.Format("{{ \"n\" : \"{0}\" }}", notificationId) +
                                                                       "}}", message,unreadCount))
                            });

							distributed = true;
						}
					}
				}
			}
			catch (Exception ex)
			{
				if (enableLogging)
					debug.Append("Error encountered:\n\n").Append(ex.ToString()).Append("\n");
			}

			if (enableLogging && debug.Length > 0)
			{
				debug.Append("Distributed: ").Append(distributed);
				PublicApi.Eventlogs.Write(debug.ToString(), new EventLogEntryWriteOptions { Category = "Push Notifications", EventId = 1234, EventType = "Information" });
			}

			return distributed;
		}

		public bool IsEnabledByDefault
		{
			get { return true; }
		}

		public string NotificationDistributionDescription
		{
			get { return _translation.GetLanguageResourceValue("distriubtion_type_description"); }
		}

		public string NotificationDistributionName
		{
			get { return _translation.GetLanguageResourceValue("distribution_type_name"); }
		}

		public Guid NotificationDistributionTypeId
		{
			get { return _id; }
		}

		#endregion

		#region IRestEndpoints Members

		public void Register(IRestEndpointController restRoutes)
		{
			restRoutes.Add(2, "mobile/pushnotifications/registration", HttpMethod.Put, request => GetRestResponse(request, true, (userId, token, device) => _deviceRegistrationService.RegisterDevice(userId.Value, token, device)));
			restRoutes.Add(2, "mobile/pushnotifications/registration", HttpMethod.Delete, request => GetRestResponse(request, false, (userId, token, device) => _deviceRegistrationService.UnregisterDevice(userId, token, device)));
		}

		IRestResponse GetRestResponse(IRestRequest request, bool requiresUser, Action<int?, string, DeviceType> action)
		{
			var token = request.Form["Token"];
			var device = (DeviceType)Enum.Parse(typeof(DeviceType), request.Form["Device"] ?? "None", true);
			int? userId = request.UserId;

			var response = new RestResponse();

			if (string.IsNullOrEmpty(token))
			{
				response.Errors = new string[] { _translation.GetLanguageResourceValue("rest_invalid_token") };
				return response;
			}

			if (device == DeviceType.None)
			{
				response.Errors = new string[] { _translation.GetLanguageResourceValue("rest_invalid_device") };
				return response;
			}

			var user = PublicApi.Users.Get(new UsersGetOptions { Id = userId });
			if (user == null || user.Errors.Count > 0)
			{
				if (requiresUser)
				{
					response.Errors = new string[] { _translation.GetLanguageResourceValue("rest_user_invalid") };
					return response;
				}
				else
				{
					userId = null;
				}
			}
			
			if (user.IsSystemAccount.HasValue && user.IsSystemAccount.Value)
			{
				// anonymous registration should be interpreted as an unregistration
				_deviceRegistrationService.UnregisterDevice(null, token, device);
			}
			else
				action(userId, token, device);

			return response;
		}

		#endregion

		#region IInstallablePlugin Members

		public void Install(Version lastInstalledVersion)
		{
			_deviceRegistrationDataService.Install();
		}

		public void Uninstall()
		{
			// nothing to uninstall.  Leave tables in place.
		}

		public Version Version
		{
			get { return GetType().Assembly.GetName().Version; }
		}

		#endregion

		#region IRequiredConfigurationPlugin Members

		public bool IsConfigured
		{
			get { return _deviceRegistrationDataService.IsConnectionStringValid(); }
		}

		#endregion
	}
}
