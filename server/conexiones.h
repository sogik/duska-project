#ifndef CONEXIONES_H
#define CONEXIONES_H

#include <mysql.h>
#include <pthread.h>

#define MAX_CONEXIONES 100

typedef struct {
    int id_jugador;
    int socket;
    char nombre_usuario[50];
    bool en_partida;
} Conexion;

// Variables globales con bloqueo mutex
extern Conexion conexiones_activas[MAX_CONEXIONES];
extern int num_conexiones_activas;
extern pthread_mutex_t mutex_conexiones;

// Funciones
void agregar_conexion(int id_jugador, int socket, const char* usuario);
void eliminar_conexion(int socket);
void broadcast_usuarios_conectados();
char* obtener_lista_usuarios();

#endif


/*
CLIENTE C SHARP



public partial class MainForm : Form {
    private TcpClient cliente;
    private NetworkStream stream;
    private Thread hiloRecepcion;
    private ListBox listaUsuarios;
    
    public MainForm() {
        InitializeComponent();
        ConectarServidor();
    }
    
    private void ConectarServidor() {
        cliente = new TcpClient("IP_SHIVA", 9050);
        stream = cliente.GetStream();
        
        hiloRecepcion = new Thread(RecibirMensajes);
        hiloRecepcion.IsBackground = true;
        hiloRecepcion.Start();
    }
    
    private void RecibirMensajes() {
        byte[] buffer = new byte[1024];
        while(true) {
            try {
                int bytesLeidos = stream.Read(buffer, 0, buffer.Length);
                string mensaje = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesLeidos);
                
                if(mensaje.StartsWith("USUARIOS_CONECTADOS:")) {
                    ActualizarListaUsuarios(mensaje.Split(':')[1].Split(','));
                }
            } catch {
                break;
            }
        }
    }
    
    private void ActualizarListaUsuarios(string[] usuarios) {
        if(InvokeRequired) {
            Invoke(new Action<string[]>(ActualizarListaUsuarios), usuarios);
            return;
        }
        
        listaUsuarios.Items.Clear();
        listaUsuarios.Items.AddRange(usuarios);
    }
    
    protected override void OnFormClosing(FormClosingEventArgs e) {
        stream.Close();
        cliente.Close();
        base.OnFormClosing(e);
    }
}


SERVIDOR C

void* manejar_cliente(void* socket_ptr);

int main() {
    int servidor_fd, cliente_fd;
    struct sockaddr_in direccion;
    int opt = 1;

    // Configurar socket del servidor
    if ((servidor_fd = socket(AF_INET, SOCK_STREAM, 0)) == 0) {
        perror("Error al crear socket");
        exit(EXIT_FAILURE);
    }

    // Configurar opciones del socket
    if (setsockopt(servidor_fd, SOL_SOCKET, SO_REUSEADDR | SO_REUSEPORT, &opt, sizeof(opt))) {
        perror("Error en setsockopt");
        exit(EXIT_FAILURE);
    }

    direccion.sin_family = AF_INET;
    direccion.sin_addr.s_addr = INADDR_ANY;
    direccion.sin_port = htons(PUERTO);

    // Vincular socket al puerto
    if (bind(servidor_fd, (struct sockaddr*)&direccion, sizeof(direccion)) < 0) {
        perror("Error en bind");
        exit(EXIT_FAILURE);
    }

    // Escuchar conexiones
    if (listen(servidor_fd, 10) < 0) {
        perror("Error en listen");
        exit(EXIT_FAILURE);
    }

    printf("Servidor escuchando en el puerto %d...\n", PUERTO);

    // Bucle principal de conexiones
    while (1) {
        socklen_t addrlen = sizeof(direccion);
        cliente_fd = accept(servidor_fd, (struct sockaddr*)&direccion, &addrlen);
        
        if (cliente_fd < 0) {
            perror("Error en accept");
            continue;
        }

        printf("Nueva conexión desde %s\n", inet_ntoa(direccion.sin_addr));

        int* socket_cliente = malloc(sizeof(int));
        *socket_cliente = cliente_fd;

        pthread_t hilo;
        if (pthread_create(&hilo, NULL, manejar_cliente, socket_cliente) != 0) {
            perror("Error al crear hilo");
            close(cliente_fd);
            free(socket_cliente);
        }
        pthread_detach(hilo);
    }

    close(servidor_fd);
    return 0;
}

void* manejar_cliente(void* socket_ptr) {
    int socket = *((int*)socket_ptr);
    char buffer[TAM_BUFFER];
    int id_jugador = -1;
    char nombre_usuario[50] = {0};
    MYSQL* conn = mysql_init(NULL);

    // Conectar a MySQL
    if (!mysql_real_connect(conn, "localhost", "usuario", "contraseña", "duska_project", 0, NULL, 0)) {
        send(socket, "ERROR_DB", 8, 0);
        close(socket);
        free(socket_ptr);
        pthread_exit(NULL);
    }

    // Autenticación
    int bytes_recibidos = recv(socket, buffer, TAM_BUFFER, 0);
    if (bytes_recibidos <= 0) {
        mysql_close(conn);
        close(socket);
        free(socket_ptr);
        pthread_exit(NULL);
    }

    buffer[bytes_recibidos] = '\0';
    char* comando = strtok(buffer, ":");
    char* usuario = strtok(NULL, ":");
    char* contrasena = strtok(NULL, ":");

    if (strcmp(comando, "LOGIN") == 0) {
        if (verificarCredenciales(conn, usuario, contrasena, &id_jugador) == 0) {
            strcpy(nombre_usuario, usuario);
            send(socket, "OK", 2, 0);
        } else {
            send(socket, "ERROR_LOGIN", 11, 0);
            mysql_close(conn);
            close(socket);
            free(socket_ptr);
            pthread_exit(NULL);
        }
    } else if (strcmp(comando, "REGISTER") == 0) {
        int resultado = registrarUsuario(conn, usuario, contrasena);
        if (resultado == 0) {
            send(socket, "OK", 2, 0);
        } else {
            send(socket, (resultado == 1) ? "USUARIO_EXISTE" : "ERROR_REGISTRO", 14, 0);
            mysql_close(conn);
            close(socket);
            free(socket_ptr);
            pthread_exit(NULL);
        }
    }

    // Registrar conexión exitosa
    if (id_jugador != -1) {
        agregar_conexion(id_jugador, socket, nombre_usuario);
        printf("Usuario conectado: %s (ID: %d)\n", nombre_usuario, id_jugador);
    }

    // Bucle de recepción de mensajes
    while (1) {
        memset(buffer, 0, TAM_BUFFER);
        int bytes_recibidos = recv(socket, buffer, TAM_BUFFER, 0);
        
        if (bytes_recibidos <= 0) {
            printf("Conexión cerrada: %s\n", nombre_usuario);
            eliminar_conexion(socket);
            break;
        }

        // Procesar otros comandos...
        if (strncmp(buffer, "LISTAR_USUARIOS", 15) == 0) {
            char lista[1024];
            listarJugadores(conn, lista, sizeof(lista));
            send(socket, lista, strlen(lista), 0);
        }
    }


*/