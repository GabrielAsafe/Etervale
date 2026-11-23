using Android.App;
using Android.OS;
using Android.Text;
using Android.Widget;
using Android.Graphics;
using PristonTale.Client.Net;

[assembly: UsesPermission(Android.Manifest.Permission.Internet)]

namespace EtervaleAndroidBootstrap;

[Activity(Label = "Etervale Android", MainLauncher = true, Exported = true)]
public class MainActivity : Activity
{
    // O servidor remoto usa um certificado de desenvolvimento autofirmado.
    // O cliente Etervale verifica o SHA-256 fixado neste endereco.
    private const string ServerAddress =
        "tcp://192.168.1.111:10009" +
        "?cert=89CCCD31A09D1218460E0610B7F03EDB3D4E3EFA35D4A721DFEAE74CC77379D7";

    private EditText _account = null!;
    private EditText _password = null!;
    private Button _loginButton = null!;
    private TextView _status = null!;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        int Dp(int n) => (int)(n * Resources!.DisplayMetrics!.Density + 0.5f);

        var content = new ScrollView(this);
        var column = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical
        };
        column.SetPadding(Dp(24), Dp(40), Dp(24), Dp(24));
        column.SetBackgroundColor(Color.Rgb(19, 27, 43));

        var title = new TextView(this)
        {
            Text = "ETERVALE ANDROID",
            TextSize = 26
        };
        title.SetTextColor(Color.White);
        column.AddView(title);

        var server = new TextView(this)
        {
            Text = "Servidor: 192.168.1.111:10009",
            TextSize = 15
        };
        server.SetTextColor(Color.LightGray);
        column.AddView(server);

        _account = new EditText(this)
        {
            Hint = "Conta",
            InputType = InputTypes.ClassText
        };
        _account.SetSingleLine(true);
        _account.SetTextColor(Color.White);
        _account.SetHintTextColor(Color.LightGray);
        column.AddView(_account);

        _password = new EditText(this)
        {
            Hint = "Palavra-passe",
            InputType = InputTypes.ClassText | InputTypes.TextVariationPassword
        };
        _password.SetSingleLine(true);
        _password.SetTextColor(Color.White);
        _password.SetHintTextColor(Color.LightGray);
        column.AddView(_password);

        _loginButton = new Button(this) { Text = "Entrar no Etervale" };
        column.AddView(_loginButton);

        _status = new TextView(this)
        {
            Text = "Introduz a conta e a palavra-passe do Etervale.",
            TextSize = 16
        };
        _status.SetTextColor(Color.White);
        column.AddView(_status);

        content.AddView(column);
        SetContentView(content);

        _loginButton.Click += async (_, _) => await TestLoginAsync();
    }

    private async Task TestLoginAsync()
    {
        string account = _account.Text?.Trim() ?? string.Empty;
        string password = _password.Text ?? string.Empty;
        if (account.Length == 0 || password.Length == 0)
        {
            _status.Text = "Preenche a conta e a palavra-passe.";
            return;
        }

        _loginButton.Enabled = false;
        _status.Text = "A autenticar no servidor Etervale...";

        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await using var backend = new GameServerConnection(ServerAddress);
            var result = await backend.LoginAsync(account, password, "pt-PT", deadline.Token);

            if (!result.Ok)
            {
                _status.Text = "Login recusado: " + result.Message;
                return;
            }

            var characters = await backend.ListCharactersAsync(deadline.Token);
            var names = characters.Count == 0
                ? "Sem personagens criadas."
                : string.Join("\n", characters.Select(c => $"- {c.Name} (nivel {c.Level})"));

            _status.Text = $"Login aprovado!\nPersonagens: {characters.Count}\n{names}\n\n" +
                "Teste concluido. A sessao sera fechada; o mundo 3D ainda nao esta integrado.";
        }
        catch (System.OperationCanceledException)
        {
            _status.Text = "Tempo esgotado na ligacao ou autenticacao.";
        }
        catch (Exception ex)
        {
            // Nunca imprimir a palavra-passe nos logs.
            _status.Text = "Falha: " + ex.GetType().Name + "\n" + ex.Message;
        }
        finally
        {
            _password.Text = string.Empty;
            _loginButton.Enabled = true;
        }
    }
}
