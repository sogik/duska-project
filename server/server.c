#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include <ctype.h>
#include <sys/types.h>
#include <sys/socket.h>
#include <netinet/in.h>
#include <unistd.h>
#include <pthread.h>
#include <errno.h>
#include "basedatos.h"
#include "auth.h"
#include "generarcartas.h"
#include "partidas.h"

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
    if (!usuario)
        return 0;

    pthread_mutex_lock(&client_list_mutex);

    ClientNode *current = client_list;
    while (current != NULL)
    {
        if (strcmp(current->usuario, usuario) == 0)
        {
            int grupo_id = current->grupo_id;
            pthread_mutex_unlock(&client_list_mutex);
            return grupo_id;
        }
        current = current->next;
    }

    pthread_mutex_unlock(&client_list_mutex);
    return 0;
}

// Función para enviar datos a todos los clientes de un grupo específico
int broadcast_to_group(int grupo_id, const char *mensaje)
{
    if (grupo_id <= 0 || !mensaje)
        return -1;

    pthread_mutex_lock(&client_list_mutex);

    int count = 0;
    ClientNode *current = client_list;
    while (current != NULL)
    {
        if (current->grupo_id == grupo_id && current->socket > 0)
        {
            write(current->socket, mensaje, strlen(mensaje));
            count++;
        }
        current = current->next;
    }

    pthread_mutex_unlock(&client_list_mutex);

    return count; // Retorna el número de usuarios a los que se envió el mensaje
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

// Función para obtener el nombre del primer usuario en un grupo (el líder)
char *obtener_lider_grupo(int grupo_id)
{
    static char lider[50];
    lider[0] = '\0';

    if (grupo_id <= 0)
        return NULL;

    pthread_mutex_lock(&client_list_mutex);

    // Buscar el primer cliente que pertenezca a este grupo
    ClientNode *current = client_list;
    while (current != NULL)
    {
        if (current->grupo_id == grupo_id)
        {
            strncpy(lider, current->usuario, sizeof(lider) - 1);
            lider[sizeof(lider) - 1] = '\0';
            break;
        }
        current = current->next;
    }

    pthread_mutex_unlock(&client_list_mutex);

    return (lider[0] != '\0') ? lider : NULL;
}

// Función para obtener el socket de un usuario por su nombre
int obtener_socket_usuario(const char *usuario)
{
    int socket = -1;

    pthread_mutex_lock(&client_list_mutex);

    ClientNode *current = client_list;
    while (current != NULL)
    {
        if (strcmp(current->usuario, usuario) == 0)
        {
            socket = current->socket;
            break;
        }
        current = current->next;
    }

    pthread_mutex_unlock(&client_list_mutex);

    return socket;
}

// Función para obtener el número de usuarios en un grupo
int num_usuarios_grupo(int grupo_id)
{
    if (grupo_id <= 0)
        return 0;

    pthread_mutex_lock(&client_list_mutex);

    int count = 0;
    ClientNode *current = client_list;
    while (current != NULL)
    {
        if (current->grupo_id == grupo_id)
            count++;

        current = current->next;
    }

    pthread_mutex_unlock(&client_list_mutex);

    return count;
}

// Función para obtener un usuario específico de un grupo por índice
char *obtener_usuario_grupo(int grupo_id, int indice)
{
    static char nombre[50];
    nombre[0] = '\0';

    if (grupo_id <= 0 || indice < 0)
        return NULL;

    pthread_mutex_lock(&client_list_mutex);

    int idx = 0;
    ClientNode *current = client_list;
    while (current != NULL)
    {
        if (current->grupo_id == grupo_id)
        {
            if (idx == indice)
            {
                strncpy(nombre, current->usuario, sizeof(nombre) - 1);
                nombre[sizeof(nombre) - 1] = '\0';
                pthread_mutex_unlock(&client_list_mutex);
                return nombre;
            }
            idx++;
        }
        current = current->next;
    }

    pthread_mutex_unlock(&client_list_mutex);
    return NULL;
}

// Función para notificar a todos los miembros de un grupo quién es el líder
void notificar_lider_grupo(int grupo_id)
{
    char *lider = obtener_lider_grupo(grupo_id);

    if (lider != NULL)
    {
        char mensaje[100];
        snprintf(mensaje, sizeof(mensaje), "LEADER/%s", lider);

        // Enviar a todos los miembros del grupo
        broadcast_to_group(grupo_id, mensaje);

        printf("Notificado líder '%s' a grupo %d\n", lider, grupo_id);
    }
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

// Función para verificar si un usuario es líder de un grupo
// Esta es usada por partidas.c
int es_lider_grupo(const char *usuario, int grupo_id)
{
    int es_lider = 0;
    pthread_mutex_lock(&client_list_mutex);

    // Buscar el primer cliente del grupo (el líder)
    ClientNode *cliente = client_list;
    while (cliente != NULL)
    {
        if (cliente->grupo_id == grupo_id)
        {
            // El primer cliente encontrado es el líder
            es_lider = (strcmp(cliente->usuario, usuario) == 0);
            break;
        }
        cliente = cliente->next;
    }

    pthread_mutex_unlock(&client_list_mutex);
    return es_lider;
}

// Función para obtener la lista de jugadores de un grupo
int listar_jugadores_grupo(int grupo_id, char jugadores[10][50])
{
    int num_jugadores = 0;
    pthread_mutex_lock(&client_list_mutex);

    ClientNode *cliente = client_list;
    while (cliente != NULL)
    {
        if (cliente->grupo_id == grupo_id && num_jugadores < 10)
        {
            strcpy(jugadores[num_jugadores], cliente->usuario);
            num_jugadores++;
        }
        cliente = cliente->next;
    }

    pthread_mutex_unlock(&client_list_mutex);
    return num_jugadores;
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

                        // AÑADIR ESTA LÍNEA: Notificar quién es el líder
                        notificar_lider_grupo(grupo_id);

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
                // Primero, enviar la lista de usuarios en el grupo
                listar_usuarios_grupo(grupo_id, respuesta, sizeof(respuesta));

                // Luego, enviar explícitamente quién es el líder
                char *lider = obtener_lider_grupo(grupo_id);
                if (lider != NULL)
                {
                    char mensaje_lider[100];
                    snprintf(mensaje_lider, sizeof(mensaje_lider), "LEADER/%s", lider);
                    // Este mensaje se enviará inmediatamente después de la respuesta actual
                    send_to_user(usuario, mensaje_lider);
                }
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

                // AÑADIR ESTA LÍNEA: Notificar cambio de líder si corresponde
                notificar_lider_grupo(grupo_id);

                strcpy(respuesta, "GRUPO_SALIDA/OK");
                printf("Usuario %s salió del grupo %d\n", usuario, grupo_id);
            }
            else
            {
                strcpy(respuesta, "ERROR/No estás en ningún grupo");
            }
        }
        // Para código 20 (iniciar partida)
        else if (codigo == 20)
        {
            // Verificar que el usuario es líder de un grupo
            int grupo_id = obtener_grupo_id(usuario);
            int es_lider = 0;

            if (grupo_id > 0)
            {
                // Verificar si es el líder
                es_lider = es_lider_grupo(usuario, grupo_id);
            }

            if (es_lider)
            {
                // Crear la partida
                int partida_id = crear_partida(grupo_id);

                if (partida_id > 0)
                {
                    // Iniciar la partida (asignar turnos aleatorios)
                    int result = iniciar_partida(partida_id);

                    if (result == 0)
                    {
                        // La notificación de inicio se envía dentro de iniciar_partida
                        strncpy(respuesta, "START_GAME_OK/Partida iniciada correctamente", sizeof(respuesta) - 1);
                    }
                    else
                    {
                        snprintf(respuesta, sizeof(respuesta), "ERROR/No se pudo iniciar la partida (código %d)", result);
                    }
                }
                else
                {
                    snprintf(respuesta, sizeof(respuesta), "ERROR/No se pudo crear la partida (código %d)", partida_id);
                }
            }
            else
            {
                strncpy(respuesta, "ERROR/Solo el líder del grupo puede iniciar la partida", sizeof(respuesta));
            }
        }
        // Para código 21 (realizar acción en el juego)
        else if (codigo == 21)
        {
            // Formato esperado: 21/usuario/accion/datos
            char accion[50] = {0};
            char datos[800] = {0};

            // El campo 'contrasena' contiene la acción
            strncpy(accion, contrasena, sizeof(accion) - 1);

            // El campo 'mensaje' contiene los datos adicionales
            if (mensaje != NULL)
            {
                strncpy(datos, mensaje, sizeof(datos) - 1);
            }

            // Verificar que es el turno del jugador
            if (es_turno_de_jugador(usuario))
            {
                // Obtener la partida del jugador
                GameInfo *partida = obtener_partida_por_jugador(usuario);

                if (partida != NULL)
                {
                    // Formato del mensaje: ACTION/jugador/accion/datos
                    char mensaje_accion[1024];
                    snprintf(mensaje_accion, sizeof(mensaje_accion), "ACTION/%s/%s/%s",
                             usuario, accion, datos);

                    // Notificar a todos los jugadores de la partida
                    broadcast_to_group(partida->grupo_id, mensaje_accion);

                    // Avanzar automáticamente al siguiente turno después de procesar la acción
                    int result = avanzar_turno(partida->partida_id);

                    if (result == 0)
                    {
                        strncpy(respuesta, "ACTION_OK/Acción procesada", sizeof(respuesta));
                    }
                    else
                    {
                        snprintf(respuesta, sizeof(respuesta),
                                 "ERROR/Acción procesada pero no se pudo avanzar el turno (código %d)",
                                 result);
                    }
                }
                else
                {
                    strncpy(respuesta, "ERROR/No estás en una partida activa", sizeof(respuesta));
                }
            }
            else
            {
                strncpy(respuesta, "ERROR/No es tu turno para realizar acciones", sizeof(respuesta));
            }
        }
        // Para código 22 (pasar turno)
        else if (codigo == 22)
        {
            // Verificar que es el turno del jugador
            if (es_turno_de_jugador(usuario))
            {
                // Obtener la partida del jugador
                GameInfo *partida = obtener_partida_por_jugador(usuario);

                if (partida != NULL)
                {
                    // Avanzar al siguiente turno
                    int result = avanzar_turno(partida->partida_id);

                    if (result == 0)
                    {
                        strncpy(respuesta, "TURN_OK/Turno pasado correctamente", sizeof(respuesta));
                    }
                    else
                    {
                        snprintf(respuesta, sizeof(respuesta), "ERROR/No se pudo avanzar el turno (código %d)", result);
                    }
                }
                else
                {
                    strncpy(respuesta, "ERROR/No estás en una partida activa", sizeof(respuesta));
                }
            }
            else
            {
                strncpy(respuesta, "ERROR/No es tu turno para pasar", sizeof(respuesta));
            }
        }
        else if (codigo == 23)
        {
            // Formato esperado: 23/usuario
            // Aquí podríamos implementar una lógica para obtener el estado de la partida
            GameInfo *partida = obtener_partida_por_jugador(usuario);
            if (partida != NULL)
            {
                char mensaje_turno[100];
                snprintf(mensaje_turno, sizeof(mensaje_turno), "TURN/%s\n",
                         partida->jugadores[partida->turno_actual]);
                printf("[TURNO] Enviando primer turno: '%s'\n", mensaje_turno);
                broadcast_to_group(partida->grupo_id, mensaje_turno);
                strcpy(respuesta, "PARTIDA/OK");
            }
            else
            {
                strcpy(respuesta, "ERROR/Error al obtener el turno");
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

    srand(time(NULL)); // Para la asignación aleatoria de turnos

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