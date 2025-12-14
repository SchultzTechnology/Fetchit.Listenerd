using Microsoft.AspNetCore.Mvc;
using Fetchit.WebPage.Models;
using Fetchit.WebPage.Services;
using Microsoft.AspNetCore.Authorization;

namespace Fetchit.WebPage.Controllers;

[Authorize]
public class MqttConfigController : Controller
{
    private readonly MqttConfigService _configService;
    private readonly SupervisorService _supervisorService;
    private readonly ConnectionSecretService _connectionSecretService;
    private readonly ILogger<MqttConfigController> _logger;

    public MqttConfigController(
        MqttConfigService configService,
        SupervisorService supervisorService,
        ConnectionSecretService connectionSecretService,
        ILogger<MqttConfigController> logger)
    {
        _configService = configService;
        _supervisorService = supervisorService;
        _connectionSecretService = connectionSecretService;
        _logger = logger;
    }

    // GET: MqttConfig
    public async Task<IActionResult> Index()
    {
        var configuration = await _configService.GetLatestConfigurationAsync();
        
        MqttSettingsViewModel model;
        if (configuration != null)
        {
            model = new MqttSettingsViewModel
            {
                ConnectionSecret = configuration.ConnectionSecret,
                BrokerPort = configuration.BrokerPort,
                TopicSubscribe = configuration.TopicSubscribe,
                TopicPublish = configuration.TopicPublish
            };
            ViewData["ConfigId"] = configuration.Id;
            ViewData["CreatedAt"] = configuration.CreatedAt;
            ViewData["UpdatedAt"] = configuration.UpdatedAt;
            ViewData["IsEdit"] = true;
            
            // Try to decode the connection secret for display
            var decoded = _connectionSecretService.DecodeConnectionSecret(configuration.ConnectionSecret);
            if (decoded != null)
            {
                ViewData["DecodedBroker"] = decoded.Broker;
                ViewData["DecodedClientId"] = decoded.ClientId;
                ViewData["DecodedLocationId"] = decoded.LocationId;
                ViewData["DecodedUserName"] = decoded.UserName;
            }
        }
        else
        {
            model = new MqttSettingsViewModel
            {
                BrokerPort = 443,
                TopicSubscribe = "",
                TopicPublish = ""
            };
            ViewData["IsEdit"] = false;
        }
        
        return View(model);
    }

    // POST: MqttConfig/Save
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(MqttSettingsViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var config = await _configService.GetLatestConfigurationAsync();
            if (config != null)
            {
                ViewData["ConfigId"] = config.Id;
                ViewData["CreatedAt"] = config.CreatedAt;
                ViewData["UpdatedAt"] = config.UpdatedAt;
                ViewData["IsEdit"] = true;
            }
            else
            {
                ViewData["IsEdit"] = false;
            }
            return View("Index", model);
        }

        try
        {
            var existingConfig = await _configService.GetLatestConfigurationAsync();
            
            if (existingConfig != null)
            {
                await _configService.UpdateConfigurationAsync(existingConfig.Id, model);
                _logger.LogInformation("MQTT configuration updated, restarting Listenerd service");
            }
            else
            {
                await _configService.SaveConfigurationAsync(model);
                _logger.LogInformation("MQTT configuration created, restarting Listenerd service");
            }
            
            // Restart the Listenerd service to pick up new configuration
            var restarted = await _supervisorService.RestartListenerdAsync();
            
            if (restarted)
            {
                TempData["SuccessMessage"] = "MQTT configuration saved and Listenerd service restarted successfully!";
                _logger.LogInformation("Listenerd service restarted successfully");
            }
            else
            {
                TempData["SuccessMessage"] = "MQTT configuration saved successfully! (Service restart not available - may require manual restart)";
                _logger.LogWarning("Failed to restart Listenerd service automatically");
            }
            
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving MQTT configuration");
            ModelState.AddModelError("", "An error occurred while saving the configuration.");
            
            var config = await _configService.GetLatestConfigurationAsync();
            if (config != null)
            {
                ViewData["ConfigId"] = config.Id;
                ViewData["CreatedAt"] = config.CreatedAt;
                ViewData["UpdatedAt"] = config.UpdatedAt;
                ViewData["IsEdit"] = true;
            }
            else
            {
                ViewData["IsEdit"] = false;
            }
            
            return View("Index", model);
        }
    }

    // POST: MqttConfig/Delete
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete()
    {
        try
        {
            var config = await _configService.GetLatestConfigurationAsync();
            if (config != null)
            {
                var result = await _configService.DeleteConfigurationAsync(config.Id);
                if (result)
                {
                    _logger.LogInformation("MQTT configuration deleted, restarting Listenerd service");
                    
                    // Restart the service after deletion
                    await _supervisorService.RestartListenerdAsync();
                    
                    TempData["SuccessMessage"] = "MQTT configuration deleted and service restarted successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Configuration not found.";
                }
            }
            else
            {
                TempData["ErrorMessage"] = "No configuration to delete.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting MQTT configuration");
            TempData["ErrorMessage"] = "An error occurred while deleting the configuration.";
        }

        return RedirectToAction(nameof(Index));
    }
}
