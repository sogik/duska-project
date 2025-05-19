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
#include "generarcartas.h"

// Estructura para mantener los clientes conectados
typedef struct ClientNode
{
    int socket;
    char usuario[50];
    int grupo_id; // ID del grupo al que pertenece el cliente, 0 significa sin grupo
    struct ClientNode *next;
} ClientNode;

ClientNode *client_list = NULL;
pthread_mutex_t client_list_mutex = PTHREAD_MUTEX_INITIALIZER;

// Contador global para asignar IDs de grupo únicos
int next_grupo_id = 1;
pthread_mutex_t grupo_id_mutex = PTHREAD_MUTEX_INITIALIZER;

// Función para obtener un nuevo ID de grupo
int get_new_grupo_id()
{
    pthread_mutex_lock(&grupo_id_mutex);
    int id = next_grupo_id++;
    pthread_mutex_unlock(&grupo_id_mutex);
    return id;
}

// Función para añadir un cliente a la lista
void add_client(int sock, const char *usuario)
{
    pthread_mutex_lock(&client_list_mutex);

    ClientNode *new_node = (ClientNode *)malloc(sizeof(ClientNode));
    new_node->socket = sock;
    strncpy(new_node->usuario, usuario, sizeof(new_node->usuario) - 1);
    new_node->usuario[sizeof(new_node->usuario) - 1] = '\0';
    new_node->grupo_id = 0; // Inicialmente sin grupo
    new_node->next = client_list;
    client_list = new_node;

    pthread_mutex_unlock(&client_list_mutex);
}

// Función para eliminar un cliente de la lista
void remove_client(int sock)
{
    pthread_mutex_lock(&client_list_mutex);

    ClientNode **pp = &client_list;
    while (*pp)
    {
        if ((*pp)->socket == sock)
        {
            ClientNode *to_free = *pp;
            *pp = (*pp)->next;
            free(to_free);
            break;
        }
        pp = &(*pp)->next;
    }

    pthread_mutex_unlock(&client_list_mutex);
}

// Función para crear un nuevo grupo con dos usuarios
int crear_grupo(const char *usuario1, const char *usuario2)
{
    int grupo_id = get_new_grupo_id();
    int usuarios_encontrados = 0;

    pthread_mutex_lock(&client_list_mutex);

    ClientNode *current = client_list;
    while (current != NULL)
    {
        if (strcmp(current->usuario, usuario1) == 0 || strcmp(current->usuario, usuario2) == 0)
        {
            current->grupo_id = grupo_id;
            usuarios_encontrados++;
        }
        current = current->next;
    }

    pthread_mutex_unlock(&client_list_mutex);

    return (usuarios_encontrados == 2) ? grupo_id : 0;
}

// Función para añadir un usuario a un grupo existente
int agregar_a_grupo(const char *usuario, int grupo_id)
{
    int exito = 0;

    pthread_mutex_lock(&client_list_mutex);

    ClientNode *current = client_list;
    while (current != NULL)
    {
        if (strcmp(current->usuario, usuario) == 0)
        {
            current->grupo_id = grupo_id;
            exito = 1;
            break;
        }
        current = current->next;
    }

    pthread_mutex_unlock(&client_list_mutex);

    return exito;
}

// Función para obtener el ID de grupo de un usuario
int obtener_grupo_id(const char *usuario)
{
    int grupo_id = 0;

    pthread_mutex_lock(&client_list_mutex);

    ClientNode *current = client_list;
    while (current != NULL)
    {
        if (strcmp(current->usuario, usuario) == 0)
        {
            grupo_id = current->grupo_id;
            break;
        }
        current = current->next;
    }

    pthread_mutex_unlock(&client_list_mutex);

    return grupo_id;
}

// Función para enviar datos a todos los clientes de un grupo específico
void broadcast_to_group(int grupo_id, const char *message)
{
    if (grupo_id <= 0)
        return; // Grupo inválido

    pthread_mutex_lock(&client_list_mutex);

    ClientNode *current = client_list;
    while (current != NULL)
    {
        if (current->grupo_id == grupo_id)
        {
            if (write(current->socket, message, strlen(message)) < 0)
            {
                perror("Error al enviar broadcast al grupo");
                // Si hay error, probablemente el cliente se desconectó
                // Nota: No eliminamos aquí para evitar problemas con el iterador
            }
        }
        current = current->next;
    }

    pthread_mutex_unlock(&client_list_mutex);
}

// Función para enviar datos a todos los clientes
void broadcast_to_all(const char *message)
{
    pthread_mutex_lock(&client_list_mutex);

    ClientNode *current = client_list;
    while (current != NULL)
    {
        if (write(current->socket, message, strlen(message)) < 0)
        {
            perror("Error al enviar broadcast");
            // Si hay error, probablemente el cliente se desconectó
            ClientNode *to_remove = current;
            current = current->next;
            remove_client(to_remove->socket);
            continue;
        }
        current = current->next;
    }

    pthread_mutex_unlock(&client_list_mutex);
}

int send_to_user(const char *destinatario, const char *mensaje)
{
    int enviado = 0;
    pthread_mutex_lock(&client_list_mutex);

    ClientNode *current = client_list;
    while (current != NULL)
    {
        if (strcmp(current->usuario, destinatario) == 0)
        {
            // Asegurarnos que el mensaje tiene un formato estándar y termina con \n
            char mensaje_formateado[1024] = {0};
            snprintf(mensaje_formateado, sizeof(mensaje_formateado), "%s\n", mensaje);

            if (write(current->socket, mensaje_formateado, strlen(mensaje_formateado)) >= 0)
            {
                enviado = 1;
            }
            break;
        }
        current = current->next;
    }

    pthread_mutex_unlock(&client_list_mutex);
    return enviado;
}

// Función para obtener la lista de usuarios en un grupo
void listar_usuarios_grupo(int grupo_id, char *buffer, int buffer_size)
{
    if (grupo_id <= 0)
    {
        snprintf(buffer, buffer_size, "ERROR/Grupo inválido");
        return;
    }

    pthread_mutex_lock(&client_list_mutex);

    char temp_buffer[1024] = {0};
    snprintf(temp_buffer, sizeof(temp_buffer), "GRUPO/%d", grupo_id);

    int count = 0;
    ClientNode *current = client_list;
    while (current != NULL)
    {
        if (current->grupo_id == grupo_id)
        {
            char usuario_info[100];
            snprintf(usuario_info, sizeof(usuario_info), "/%s", current->usuario);
            strcat(temp_buffer, usuario_info);
            count++;
        }
        current = current->next;
    }

    pthread_mutex_unlock(&client_list_mutex);

    if (count > 0)
    {
        strncpy(buffer, temp_buffer, buffer_size - 1);
        buffer[buffer_size - 1] = '\0';
    }
    else
    {
        snprintf(buffer, buffer_size, "ERROR/No hay usuarios en este grupo");
    }
}

void *cliente(void *socket_ptr)
{
    int sock_conn = *((int *)socket_ptr);
    free(socket_ptr); // Liberamos la memoria ahora que tenemos el valor
    char peticion[512];
    char respuesta[1024];
    int ret;
    char usuario[50] = {0}; // Para mantener el nombre de usuario

    MYSQL *conn = mysql_init(NULL);
    if (!mysql_real_connect(conn, "shiva2.upc.es", "root", "mysql", "duska_project", 0, NULL, 0))
    {
        printf("Error MySQL: %s\n", mysql_error(conn));
        strcpy(respuesta, "Error en la base de datos");
        write(sock_conn, respuesta, strlen(respuesta));
        close(sock_conn);
        return NULL;
    }

    while (1)
    {
        ret = read(sock_conn, peticion, sizeof(peticion) - 1);
        if (ret <= 0)
        {
            // Cliente desconectado
            if (strlen(usuario) > 0)
            {
                // Obtener el grupo del usuario antes de eliminarlo
                int grupo_id = obtener_grupo_id(usuario);

                // Actualizar estado como desconectado
                actualizarEstado(conn, usuario, 0);

                // Enviar lista actualizada a todos
                char buffer[1024] = {0};
                listarConectados(conn, buffer, sizeof(buffer));
                broadcast_to_all(buffer);

                // Si estaba en un grupo, notificar a los demás miembros
                if (grupo_id > 0)
                {
                    char notify_message[256];
                    snprintf(notify_message, sizeof(notify_message), "GRUPO_SALIDA/%s", usuario);
                    broadcast_to_group(grupo_id, notify_message);
                }

                // Eliminar de la lista de clientes
                remove_client(sock_conn);
            }
            break;
        }
        peticion[ret] = '\0';
        printf("Peticion: %s\n", peticion);

        char *p = strtok(peticion, "/");
        int codigo = atoi(p);
        p = strtok(NULL, "/");
        if (p != NULL)
        {
            strncpy(usuario, p, sizeof(usuario) - 1);
            usuario[sizeof(usuario) - 1] = '\0';
        }
        p = strtok(NULL, "/");
        char contrasena[72] = {0};
        if (p != NULL)
        {
            strncpy(contrasena, p, sizeof(contrasena) - 1);
            contrasena[sizeof(contrasena) - 1] = '\0';
        }
        char mensaje[900] = {0};
        if (p != NULL)
        {
            p = strtok(NULL, "/");
            if (p != NULL)
            {
                strncpy(mensaje, p, sizeof(mensaje) - 1);
                mensaje[sizeof(mensaje) - 1] = '\0';
            }
        }
        else
        {
            p = NULL;
            mensaje[0] = '\0';
        }

        memset(respuesta, 0, sizeof(respuesta));

        if (codigo == 0)
        {
            int reg_result = registrarUsuario(conn, usuario, contrasena);
            snprintf(respuesta, sizeof(respuesta), "%d", reg_result);
        }
        else if (codigo == 1)
        {
            int login_result = iniciarSesion(conn, usuario, contrasena);
            if (login_result == 0)
            {
                add_client(sock_conn, usuario); // Añadir a la lista de clientes
            }
            snprintf(respuesta, sizeof(respuesta), "%d", login_result);
        }
        else if (codigo == 2)
        {
            listarJugadores(conn, respuesta, sizeof(respuesta));
        }
        else if (codigo == 3)
        {
            listarPartidas(conn, respuesta, sizeof(respuesta));
        }
        else if (codigo == 4)
        {
            listarPartidasGanadas(conn, usuario, respuesta, sizeof(respuesta));
        }
        else if (codigo == 5)
        {
            listarConectados(conn, respuesta, sizeof(respuesta));
        }
        else if (codigo == 6)
        {
            int estado = atoi(contrasena);
            int update_result = actualizarEstado(conn, usuario, estado);

            if (update_result == 0)
            {
                char buffer[1024] = {0};
                listarConectados(conn, buffer, sizeof(buffer));
                strcpy(respuesta, buffer);

                // Enviar la lista actualizada a todos los clientes
                broadcast_to_all(buffer);
            }
            else
            {
                strcpy(respuesta, "ERROR/Error al actualizar estado");
            }
        }
        else if (codigo == 7)
        {
            // Formato esperado: 7/remitente/destinatario/mensaje
            char destinatario[50] = {0};
            char mensaje_completo[900] = {0};

            // El destinatario estará en la variable 'contrasena'
            strncpy(destinatario, contrasena, sizeof(destinatario) - 1);

            if (mensaje != NULL)
            {
                strncpy(mensaje_completo, mensaje, sizeof(mensaje_completo) - 1);

                // Formato del mensaje que se enviará: "MSG/remitente/mensaje"
                char mensaje_final[1024] = {0};
                snprintf(mensaje_final, sizeof(mensaje_final), "INV/%s", usuario);

                int result = send_to_user(destinatario, mensaje_final);

                if (result == 1)
                {
                    strcpy(respuesta, "INV2/Invitacion enviada correctamente");
                    printf("Invitación enviada a %s\n", destinatario);
                }
                else
                {
                    strcpy(respuesta, "INV2/El usuario no está conectado o hubo un error");
                }
            }
            else
            {
                strcpy(respuesta, "ERROR/Formato de mensaje inválido");
            }
        }
        else if (codigo == 8)
        {
            // Formato esperado: 8/remitente/destinatario/respuesta
            char destinatario[50] = {0};
            char respuesta_inv[900] = {0};

            // El destinatario estará en la variable 'contrasena'
            strncpy(destinatario, contrasena, sizeof(destinatario) - 1);

            if (mensaje != NULL)
            {
                strncpy(respuesta_inv, mensaje, sizeof(respuesta_inv) - 1);

                // Formato del mensaje que se enviará: "INVR/remitente/respuesta"
                char mensaje_final[1024] = {0};
                snprintf(mensaje_final, sizeof(mensaje_final), "INVR/%s/%s", usuario, respuesta_inv);

                int result = send_to_user(destinatario, mensaje_final);

                // Si la respuesta es "ACEPTADA", crear un grupo
                if (result == 1 && strcmp(respuesta_inv, "ACEPTADA") == 0)
                {
                    int grupo_id = crear_grupo(usuario, destinatario);
                    if (grupo_id > 0)
                    {
                        // Notificar a ambos usuarios sobre la creación del grupo
                        char grupo_msg[256];
                        snprintf(grupo_msg, sizeof(grupo_msg), "GRUPO_CREADO/%d", grupo_id);
                        send_to_user(usuario, grupo_msg);
                        send_to_user(destinatario, grupo_msg);

                        // Enviar la lista de usuarios en el grupo
                        char lista_grupo[1024];
                        listar_usuarios_grupo(grupo_id, lista_grupo, sizeof(lista_grupo));
                        broadcast_to_group(grupo_id, lista_grupo);

                        printf("Grupo %d creado para %s y %s\n", grupo_id, usuario, destinatario);
                    }
                    else
                    {
                        printf("Error al crear grupo para %s y %s\n", usuario, destinatario);
                    }
                }

                if (result == 1)
                {
                    strcpy(respuesta, "INVR2/Respuesta enviada correctamente");
                    printf("Respuesta de invitación enviada a %s\n", destinatario);
                }
                else
                {
                    strcpy(respuesta, "INVR2/El usuario no está conectado o hubo un error");
                }
            }
            else
            {
                strcpy(respuesta, "ERROR/Formato de mensaje inválido");
            }
        }
        else if (codigo == 9)
        {
            char mensaje_completo[900] = {0};

            if (mensaje != NULL)
            {
                strncpy(mensaje_completo, mensaje, sizeof(mensaje_completo) - 1);

                char *cartas = generar_cartas_aleatorias();

                snprintf(respuesta, sizeof(respuesta), "%s", cartas);
            }
            else
            {
                strcpy(respuesta, "ERROR/Formato de mensaje inválido");
            }
        }
        else if (codigo == 10)
        {
            // Formato esperado: 10/remitente/destinatario/mensaje
            char destinatario[50] = {0};
            char mensaje_completo[900] = {0};

            // El destinatario estará en la variable 'contrasena'
            strncpy(destinatario, contrasena, sizeof(destinatario) - 1);

            if (mensaje != NULL)
            {
                strncpy(mensaje_completo, mensaje, sizeof(mensaje_completo) - 1);

                // Formato del mensaje que se enviará: "CHAT/remitente"
                char mensaje_final[1024] = {0};
                snprintf(mensaje_final, sizeof(mensaje_final), "CHAT/%s", usuario);

                int result = send_to_user(destinatario, mensaje_final);

                if (result == 1)
                {
                    strcpy(respuesta, "CHAT/Mensaje enviado correctamente");
                    printf("Mensaje enviado a %s\n", destinatario);
                }
                else
                {
                    strcpy(respuesta, "CHAT/El usuario no está conectado o hubo un error");
                }
            }
            else
            {
                strcpy(respuesta, "ERROR/Formato de mensaje inválido");
            }
        }
        // Nuevo código para broadcast a un grupo específico
        else if (codigo == 11)
        {
            // Formato esperado: 11/remitente/mensaje
            // Usamos el campo contrasena para indicar el mensaje
            char mensaje_grupo[900] = {0};
            strncpy(mensaje_grupo, contrasena, sizeof(mensaje_grupo) - 1);

            // Obtener el grupo del usuario
            int grupo_id = obtener_grupo_id(usuario);

            if (grupo_id > 0)
            {
                // Formato del mensaje que se enviará: "GRUPO_MSG/remitente/mensaje"
                char mensaje_final[1024] = {0};
                snprintf(mensaje_final, sizeof(mensaje_final), "CHAT/%s/%s", usuario, mensaje_grupo);

                // Broadcast al grupo
                broadcast_to_group(grupo_id, mensaje_final);

                strcpy(respuesta, "MSG/OK");
                printf("Mensaje de grupo enviado por %s al grupo %d\n", usuario, grupo_id);
            }
            else
            {
                strcpy(respuesta, "ERROR/No estás en ningún grupo");
            }
        }
        // Obtener la lista de usuarios en el grupo
        else if (codigo == 12)
        {
            // Obtener el grupo del usuario
            int grupo_id = obtener_grupo_id(usuario);

            if (grupo_id > 0)
            {
                listar_usuarios_grupo(grupo_id, respuesta, sizeof(respuesta));
            }
            else
            {
                strcpy(respuesta, "ERROR/No estás en ningún grupo");
            }
        }
        // Salir del grupo
        else if (codigo == 13)
        {
            // Obtener el grupo del usuario
            int grupo_id = obtener_grupo_id(usuario);

            if (grupo_id > 0)
            {
                // Notificar a los demás miembros del grupo
                char mensaje_salida[256];
                snprintf(mensaje_salida, sizeof(mensaje_salida), "GRUPO_SALIDA/%s", usuario);
                broadcast_to_group(grupo_id, mensaje_salida);

                // Eliminar al usuario del grupo
                pthread_mutex_lock(&client_list_mutex);
                ClientNode *current = client_list;
                while (current != NULL)
                {
                    if (strcmp(current->usuario, usuario) == 0)
                    {
                        current->grupo_id = 0;
                        break;
                    }
                    current = current->next;
                }
                pthread_mutex_unlock(&client_list_mutex);

                strcpy(respuesta, "GRUPO_SALIDA/OK");
                printf("Usuario %s salió del grupo %d\n", usuario, grupo_id);
            }
            else
            {
                strcpy(respuesta, "ERROR/No estás en ningún grupo");
            }
        }
        else
        {
            strcpy(respuesta, "ERROR/Comando desconocido");
        }

        printf("Resultado: %s\n", respuesta);
        if (write(sock_conn, respuesta, strlen(respuesta)) < 0)
        {
            perror("Error al escribir en socket");
            break;
        }
    }

    mysql_close(conn);
    close(sock_conn);
    return NULL;
}

int main(int argc, char *argv[])
{
    int sock_conn, sock_listen;
    struct sockaddr_in serv_adr;

    if ((sock_listen = socket(AF_INET, SOCK_STREAM, 0)) < 0)
    {
        perror("Error creant socket");
        exit(1);
    }

    memset(&serv_adr, 0, sizeof(serv_adr));
    serv_adr.sin_family = AF_INET;
    serv_adr.sin_addr.s_addr = htonl(INADDR_ANY);
    serv_adr.sin_port = htons(50756);

    if (bind(sock_listen, (struct sockaddr *)&serv_adr, sizeof(serv_adr)) < 0)
    {
        perror("Error al bind");
        close(sock_listen);
        exit(1);
    }

    if (listen(sock_listen, 10) < 0)
    {
        perror("Error en el listen");
        close(sock_listen);
        exit(1);
    }

    printf("Servidor escuchando en el puerto 50756...\n");

    while (1)
    {
        sock_conn = accept(sock_listen, NULL, NULL);
        if (sock_conn < 0)
        {
            perror("Error en accept");
            continue;
        }
        printf("Nuevo cliente conectado.\n");

        int *socket_ptr = malloc(sizeof(int));
        *socket_ptr = sock_conn;

        pthread_t hilo;
        if (pthread_create(&hilo, NULL, cliente, socket_ptr) != 0)
        {
            perror("Error al crear hilo");
            close(sock_conn);
            free(socket_ptr);
        }
        else
        {
            pthread_detach(hilo);
        }
    }

    close(sock_listen);
    return 0;
}