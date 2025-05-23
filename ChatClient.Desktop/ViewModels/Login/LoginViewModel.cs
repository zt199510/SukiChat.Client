using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using Avalonia.Notification;
using ChatClient.BaseService.Services;
using ChatClient.Desktop.Tool;
using ChatClient.Desktop.Views.Login;
using ChatClient.Desktop.Views.UserControls;
using ChatClient.Tool.Common;
using ChatClient.Tool.Data;
using ChatClient.Tool.HelperInterface;
using ChatClient.Tool.ManagerInterface;
using ChatServer.Common.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Prism.Commands;
using Prism.Dialogs;
using Prism.Ioc;
using Prism.Navigation.Regions;

namespace ChatClient.Desktop.ViewModels.Login;

public class LoginViewModel : ViewModelBase, IDisposable
{
    #region IsConnected

    private Connect _isConnected;

    public Connect IsConnected
    {
        get => _isConnected;
        set => SetProperty(ref _isConnected, value);
    }

    #endregion

    #region IsBusy

    private bool isBusy = false;

    public bool IsBusy
    {
        get => isBusy;
        set
        {
            SetProperty(ref isBusy, value);
            LoginCommand.RaiseCanExecuteChanged();
        }
    }

    #endregion

    #region 昵称(Name)

    private string? id;

    public string? Id
    {
        get => id;
        set
        {
            if (SetProperty(ref id, value))
            {
                if (value != null && LoginData.RememberPassword) GetPassword(value);
                LoginCommand.RaiseCanExecuteChanged();
            }
        }
    }

    #endregion

    #region 密码(Password)

    private string? password;

    public string? Password
    {
        get => password;
        set
        {
            if (SetProperty(ref password, value))
            {
                LoginCommand.RaiseCanExecuteChanged();
                autoPassword = false;
            }
        }
    }

    #endregion

    #region 记住密码

    private LoginData _loginData;

    public LoginData LoginData
    {
        get => _loginData;
        set => SetProperty(ref _loginData, value);
    }

    #endregion

    private bool autoPassword = false;

    public AsyncDelegateCommand LoginCommand { get; init; }
    public DelegateCommand ToRegisterViewCommand { get; init; }
    public DelegateCommand ToForgetViewCommand { get; init; }

    public INotificationMessageManager NotificationManager { get; init; } = new NotificationMessageManager();

    private readonly IRegionManager _regionManager;
    private readonly IContainerProvider _containerProvider;
    private readonly IDialogService _dialogService;

    public LoginViewModel(IConnection connection,
        IRegionManager regionManager,
        ILoginData loginData,
        IContainerProvider containerProvider,
        IDialogService dialogService)
    {
        _regionManager = regionManager;
        _containerProvider = containerProvider;
        _dialogService = dialogService;
        LoginData = loginData.LoginData;
        IsConnected = connection.IsConnected;

        LoginCommand = new AsyncDelegateCommand(Login, CanLogin);
        ToRegisterViewCommand = new DelegateCommand(ToRegisterView);
        ToForgetViewCommand = new DelegateCommand(ToForgetView);

        IsConnected.PropertyChanged += IsConnectedOnPropertyChanged;

        Id = LoginData.Id;
    }

    private void IsConnectedOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        LoginCommand.RaiseCanExecuteChanged();
    }


    private bool CanLogin() => IsConnected.IsConnected && !string.IsNullOrEmpty(Id) &&
                               !string.IsNullOrEmpty(Password) && !isBusy;

    private async Task Login()
    {
        IsBusy = true;

        CommonResponse? result = null;
        var _userManager = _containerProvider.Resolve<IUserManager>();
        result = await _userManager.Login(Id!, Password!, LoginData.RememberPassword);

        // 登录成功
        if (result is { State: true })
        {
            // 设置最近一次登录的账号
            LoginData.Id = Id;

            Id = null;
            Password = null;

            TranslateWindowHelper.TranslateToMainWindow(_regionManager);
        }
        else
        {
            string title = result == null ? "网络错误" : result.Message;
            NotificationManager.ShowMessage(title, NotificationType.Error, TimeSpan.FromSeconds(1.5));
            Password = null;
        }

        IsBusy = false;
    }

    private async void GetPassword(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length != 10)
        {
            if (autoPassword)
                Password = null;
        }

        var _loginService = _containerProvider.Resolve<ILoginService>();
        var result = await _loginService.GetPassword(id);
        if (_loginService is IDisposable disposable)
            disposable.Dispose();

        if (!string.IsNullOrEmpty(result))
        {
            Password = result;
            autoPassword = true;
        }
        else if (autoPassword)
            Password = null;
    }

    private void ToRegisterView()
    {
        _dialogService.Show(nameof(RegisterWindowView), res =>
        {
            if (res.Result == ButtonResult.OK)
            {
                Id = res.Parameters["ID"] as string;
                Password = null;
                LoginData.Id = Id;
            }
        });
    }

    private void ToForgetView()
    {
        // var cornerDialogManager = _containerProvider.Resolve<ICornerDialogService>();
        // NotificationManager.ShowMessage("功能未开放", NotificationType.Information, TimeSpan.FromSeconds(1.5));
        _dialogService.Show(nameof(ForgetPasswordView), res =>
        {
            if (res.Result == ButtonResult.OK)
            {
                Id = res.Parameters["ID"] as string;
                Password = null;
                LoginData.Id = Id;
            }
        });
    }

    #region Dispose

    private bool _isDisposed = false;

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            if (disposing)
            {
                // 释放托管资源
                IsConnected.PropertyChanged -= IsConnectedOnPropertyChanged;
            }

            _isDisposed = true;
        }
    }

    /// <summary>
    /// 析构函数，作为安全网
    /// </summary>
    ~LoginViewModel()
    {
        Dispose(false);
    }

    #endregion
}