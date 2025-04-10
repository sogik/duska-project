#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include <ctype.h>
#include <mysql.h>
#include <sys/types.h>
#include <sys/socket.h>
#include <netinet/in.h>
#include <unistd.h>
#include <pthread.h>
#include <errno.h>

#include "basedatos.h"
#include "auth.h"

#define MAX_CLIENTES 100

int clientes[MAX_CLIENTES];
int num_clientes = 0;
pthread_mutex_t mutex_clientes = PTHREAD_MUTEX_INITIALIZER;

// Función para enviar un mensaje a todos los clientes conectados
void enviarATodos(const char* mensaje) {
    pthread_mutex_lock(&mutex_clientes);
    printf("Enviando mensaje a todos los clientes: %s\n", mensaje);

    // Crear un buffer con el mensaje y un '\n' al final
    char mensajeConNuevaLinea[1024];
    snprintf(mensajeConNuevaLinea, sizeof(mensajeConNuevaLinea), "%s\n", mensaje);

    for (int i = 0; i < num_clientes; ) {
        if (clientes[i] != -1) {
            int r = write(clientes[i], mensajeConNuevaLinea, strlen(mensajeConNuevaLinea));
            if (r < 0) {
                perror("Error enviando a cliente");
                if (errno == EPIPE) {
                    printf("Cliente %d desconectado.\n", clientes[i]);
                    // Mover los clientes restantes hacia adelante
                    for (int j = i; j < num_clientes - 1; j++) {
                        clientes[j] = clientes[j + 1];
                    }
                    num_clientes--;
                    continue; // No incrementar `i` porque ya hemos movido los clientes
                }
            } else {
                printf("Mensaje enviado correctamente al cliente %d\n", clientes[i]);
            }
        }
        i++;
    }
    pthread_mutex_unlock(&mutex_clientes);
}

// Función que maneja la conexión de un cliente
void* cliente(void* socket_ptr) {
    int sock_conn = *((int*)socket_ptr);
    char peticion[512];
    char respuesta[1024];
    int ret;

    // Leer la petición del cliente
    int pos = 0;
    char c;
    while (read(sock_conn, &c, 1) > 0 && c != '\n' && pos < sizeof(peticion) - 1) {
        peticion[pos++] = c;
    }
    peticion[pos] = '\0';

    printf("Peticion: %s\n", peticion);

    MYSQL *conn = mysql_init(NULL);
    if (!mysql_real_connect(conn, "shiva2.upc.es", "root", "mysql", "duska_project", 0, NULL, 0)) {
        printf("Error MySQL: %s\n", mysql_error(conn));
        strcpy(respuesta, "Error en la base de datos\n");
        write(sock_conn, respuesta, strlen(respuesta));
        close(sock_conn);
        free(socket_ptr);
        return NULL;
    }

    // Parsear la petición
    char *p = strtok(peticion, "/");
    int codigo = atoi(p);

    p = strtok(NULL, "/");
    char usuario[50] = "";
    if (p) strcpy(usuario, p);

    p = strtok(NULL, "/");
    char contrasena[72] = "";
    if (p) strcpy(contrasena, p);

    // Manejar las diferentes peticiones
    if (codigo == 0) { // Registrar usuario
        int res = registrarUsuario(conn, usuario, contrasena);
        sprintf(respuesta, "%d\n", res);
    } 
    else if (codigo == 1) { // Iniciar sesión
        int res = iniciarSesion(conn, usuario, contrasena);
        sprintf(respuesta, "%d\n", res);
    } 
    else if (codigo == 2) { // Listar jugadores
        listarJugadores(conn, respuesta, sizeof(respuesta));
        strcat(respuesta, "\n");
    } 
    else if (codigo == 3) { // Listar partidas
        listarPartidas(conn, respuesta, sizeof(respuesta));
        strcat(respuesta, "\n");
    } 
    else if (codigo == 4) { // Listar partidas ganadas
        listarPartidasGanadas(conn, usuario, respuesta, sizeof(respuesta));
        strcat(respuesta, "\n");
    }
    else if (codigo == 5) { // Listar conectados
        listarConectados(conn, respuesta, sizeof(respuesta));
        strcat(respuesta, "\n");
    }
    else if (codigo == 6) { // Actualizar estado y enviar lista a todos
        char buffer[1024] = {0};
        int estado = actualizarEstado(conn, usuario, atoi(contrasena));
        printf("Estado: %d\n", estado);

        if (estado == 0) {
            listarConectados(conn, buffer, sizeof(buffer));
            printf("Buffer conectados: %s\n", buffer);

            // Enviar a todos los clientes conectados
            enviarATodos(buffer);
        } else {
            strcpy(respuesta, "Error al cambiar estado\n");
        }
    }

    printf("Resultado: %s\n", respuesta);
    write(sock_conn, respuesta, strlen(respuesta));

    // Eliminar el cliente de la lista y cerrar la conexión
    pthread_mutex_lock(&mutex_clientes);
    for (int i = 0; i < num_clientes; i++) {
        if (clientes[i] == sock_conn) {
            for (int j = i; j < num_clientes - 1; j++) {
                clientes[j] = clientes[j + 1];
            }
            num_clientes--;
            printf("Cliente eliminado de la lista. Total clientes: %d\n", num_clientes);
            break;
        }
    }
    pthread_mutex_unlock(&mutex_clientes);

    close(sock_conn);
    free(socket_ptr);

    return NULL;
}

// Función principal del servidor
int main(int argc, char *argv[]) {
    int sock_conn, sock_listen;
    struct sockaddr_in serv_adr;

    if ((sock_listen = socket(AF_INET, SOCK_STREAM, 0)) < 0) {
        perror("Error creando socket");
        exit(1);
    }

    memset(&serv_adr, 0, sizeof(serv_adr));
    serv_adr.sin_family = AF_INET;
    serv_adr.sin_addr.s_addr = htonl(INADDR_ANY);
    serv_adr.sin_port = htons(50756);

    if (bind(sock_listen, (struct sockaddr *) &serv_adr, sizeof(serv_adr)) < 0) {
        perror("Error en bind");
        close(sock_listen);
        exit(1);
    }

    if (listen(sock_listen, 10) < 0) {
        perror("Error en listen");
        close(sock_listen);
        exit(1);
    }

    printf("Servidor escuchando en el puerto 50756...\n");

    while (1) {
        sock_conn = accept(sock_listen, NULL, NULL);
        if (sock_conn < 0) {
            perror("Error en accept");
            continue;
        }
        printf("Nuevo cliente conectado.\n");

        // Agregar el cliente a la lista
        pthread_mutex_lock(&mutex_clientes);
        if (num_clientes < MAX_CLIENTES) {
            clientes[num_clientes++] = sock_conn;
            printf("Cliente añadido a la lista. Total clientes: %d\n", num_clientes);
        } else {
            printf("Máximo número de clientes alcanzado. Rechazando conexión.\n");
            close(sock_conn);
            pthread_mutex_unlock(&mutex_clientes);
            continue;
        }
        pthread_mutex_unlock(&mutex_clientes);

        // Crear un hilo para manejar al cliente
        int* socket_ptr = malloc(sizeof(int));
        *socket_ptr = sock_conn;

        pthread_t hilo;
        if (pthread_create(&hilo, NULL, cliente, socket_ptr) != 0) {
            perror("Error al crear hilo");
            close(sock_conn);
            free(socket_ptr);
        } else {
            pthread_detach(hilo);
        }
    }

    close(sock_listen);
    return 0;
}