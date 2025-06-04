#include <mysql.h>
#include <string.h>
#include <stdlib.h>
#include <stdio.h>
#include "basedatos.h"

int desconectar_base_datos(MYSQL *conn)
{
    mysql_close(conn);
    return 0;
}

int usuarioExiste(MYSQL *conn, const char *nombre_usuario)
{
    MYSQL_RES *res;
    MYSQL_ROW row;

    char consulta[256];

    snprintf(consulta, sizeof(consulta), "SELECT nombre_usuario FROM Jugadores WHERE nombre_usuario = '%s'", nombre_usuario);

    int err = mysql_query(conn, consulta);
    if (err != 0)
    {
        printf("Error al consultar datos de la base %u %s\n", mysql_errno(conn), mysql_error(conn));
        exit(1);
    }

    res = mysql_store_result(conn);
    if (res)
    {
        row = mysql_fetch_row(res);
        mysql_free_result(res);

        if (row)
        {
            return 1;
        }
    }

    return 0;
}

int actualizarEstado(MYSQL *conn, const char *nombre_usuario, int estado)
{
    char consulta[256];

    snprintf(consulta, sizeof(consulta), "UPDATE Jugadores SET estado = '%d' WHERE nombre_usuario = '%s'", estado, nombre_usuario);

    int err = mysql_query(conn, consulta);
    if (err != 0)
    {
        printf("Error al actualizar los datos en la base %u %s\n", mysql_errno(conn), mysql_error(conn));
        exit(1);
    }

    return 0;
}

int insertarUsuario(MYSQL *conn, const char *nombre_usuario, const char *contrasena)
{
    char consulta[256];

    snprintf(consulta, sizeof(consulta), "INSERT INTO Jugadores (nombre_usuario, contrasena) VALUES ('%s', '%s')", nombre_usuario, contrasena);

    int err = mysql_query(conn, consulta);
    if (err != 0)
    {
        printf("Error al introducir datos en la base %u %s\n", mysql_errno(conn), mysql_error(conn));
        exit(1);
    }

    return 0;
}

int eliminarUsuario(MYSQL *conn, const char *nombre_usuario)
{
    char consulta[256];

    // Primero eliminar registros dependientes en Partida_Jugadores
    snprintf(consulta, sizeof(consulta), 
             "DELETE FROM Partida_Jugadores WHERE nombre_usuario = '%s'", 
             nombre_usuario);

    int err = mysql_query(conn, consulta);
    if (err != 0)
    {
        printf("Error al eliminar registros de Partida_Jugadores: %u %s\n", 
               mysql_errno(conn), mysql_error(conn));
        return -1;
    }

    printf("Registros de Partida_Jugadores eliminados para '%s'\n", nombre_usuario);

    // Luego eliminar el usuario de la tabla Jugadores
    snprintf(consulta, sizeof(consulta), 
             "DELETE FROM Jugadores WHERE nombre_usuario = '%s'", 
             nombre_usuario);

    err = mysql_query(conn, consulta);
    if (err != 0)
    {
        printf("Error al eliminar usuario de la base %u %s\n", 
               mysql_errno(conn), mysql_error(conn));
        return -1;
    }

    if (mysql_affected_rows(conn) > 0)
    {
        printf("Usuario '%s' eliminado exitosamente de la base de datos\n", nombre_usuario);
        return 0;
    }
    else
    {
        printf("No se encontró el usuario '%s' para eliminar\n", nombre_usuario);
        return -2;
    }
}

int verificarCredenciales(MYSQL *conn, const char *nombre_usuario, const char *contrasena)
{
    MYSQL_RES *res;
    MYSQL_ROW row;

    char consulta[256];

    snprintf(consulta, sizeof(consulta), "SELECT contrasena FROM Jugadores WHERE nombre_usuario = '%s'", nombre_usuario);

    int err = mysql_query(conn, consulta);
    if (err != 0)
    {
        printf("Error al consultar datos de la base %u %s\n", mysql_errno(conn), mysql_error(conn));
        exit(1);
    }

    res = mysql_store_result(conn);
    if (res)
    {
        row = mysql_fetch_row(res);
        mysql_free_result(res);

        if (row)
        {
            if (strcmp(row[0], contrasena) == 0)
            {
                return 0;
            }
        }
    }

    return 1;
}

void listarJugadores(MYSQL *conn, char *lista, int tamano_lista)
{
    MYSQL_RES *res;
    MYSQL_ROW row;

    lista[0] = '\0';

    int err = mysql_query(conn, "SELECT nombre_usuario FROM Jugadores");
    if (err != 0)
    {
        printf("Error al consultar: %s\n", mysql_error(conn));
        return;
    }

    res = mysql_use_result(conn);
    if (res)
    {
        strcat(lista, "LISTU/");
        while ((row = mysql_fetch_row(res)))
        {
            strcat(lista, row[0]);
            strcat(lista, "/");
        }
        mysql_free_result(res);
    }
    else
    {
        printf("Error al obtener el resultado: %s\n", mysql_error(conn));
    }
}

void listarPartidas(MYSQL *conn, char *lista, int tamano_lista)
{
    MYSQL_RES *res;
    MYSQL_ROW row;

    lista[0] = '\0';

    int err = mysql_query(conn, "SELECT * FROM Partidas");
    if (err != 0)
    {
        printf("Error al consultar: %s\n", mysql_error(conn));
        return;
    }

    res = mysql_use_result(conn);
    if (res)
    {

        if (mysql_num_rows(res) == 0)
        {
            printf("La tabla Partidas está vacía.\n");
            mysql_free_result(res);
            return;
        }
        while ((row = mysql_fetch_row(res)))
        {
            strcat(lista, row[0]);
            strcat(lista, " ");
            strcat(lista, row[1]);
            strcat(lista, " ");
            strcat(lista, row[2]);
            strcat(lista, " ");
            strcat(lista, row[3]);
            strcat(lista, " ");
            strcat(lista, row[4]);
            strcat(lista, "\n");
        }
        mysql_free_result(res);
    }
    else
    {
        printf("Error al obtener el resultado: %s\n", mysql_error(conn));
    }
}

int obtenerIdJugadorPorNombre(MYSQL *conn, const char *nombre_usuario)
{
    MYSQL_RES *res;
    MYSQL_ROW row;
    char consulta[256];
    int id_jugador = -1;

    snprintf(consulta, sizeof(consulta),
             "SELECT id_jugador FROM Jugadores WHERE nombre_usuario = '%s'",
             nombre_usuario);

    int err = mysql_query(conn, consulta);
    if (err != 0)
    {
        printf("Error al consultar ID del jugador: %s\n", mysql_error(conn));
        return -1;
    }

    res = mysql_store_result(conn);
    if (res)
    {
        row = mysql_fetch_row(res);
        if (row)
        {
            id_jugador = atoi(row[0]);
        }
        mysql_free_result(res);
    }

    return id_jugador;
}

int insertarPartida(MYSQL *conn, int num_jugadores, char jugadores[10][50])
{
    char consulta[1024];
    int partida_id = -1;

    // **CONSTRUIR CADENA DE JUGADORES SEPARADOS POR COMAS**
    char jugadores_str[500] = {0};
    for (int i = 0; i < num_jugadores; i++)
    {
        if (i > 0)
        {
            strcat(jugadores_str, ",");
        }
        strcat(jugadores_str, jugadores[i]);
    }

    printf("[BD] Jugadores para insertar: '%s'\n", jugadores_str);

    // **INSERTAR CON JUGADORES**
    snprintf(consulta, sizeof(consulta),
             "INSERT INTO Partidas (num_jugadores, jugadores, estado, fecha_inicio) VALUES (%d, '%s', 'ACTIVA', NOW())",
             num_jugadores, jugadores_str);

    int err = mysql_query(conn, consulta);
    if (err != 0)
    {
        printf("Error al insertar partida: %s\n", mysql_error(conn));
        return -1;
    }

    // Obtener el ID de la partida recién creada
    partida_id = (int)mysql_insert_id(conn);
    printf("[BD] Partida creada con ID: %d, jugadores: %s\n", partida_id, jugadores_str);

    // **TAMBIÉN INSERTAR EN PARTIDA_JUGADORES COMO ANTES**
    for (int i = 0; i < num_jugadores; i++)
    {
        int id_jugador = obtenerIdJugadorPorNombre(conn, jugadores[i]);

        if (id_jugador > 0)
        {
            snprintf(consulta, sizeof(consulta),
                     "INSERT INTO Partida_Jugadores (id_partida, id_jugador, nombre_usuario, posicion_turno) "
                     "VALUES (%d, %d, '%s', %d)",
                     partida_id, id_jugador, jugadores[i], i);

            err = mysql_query(conn, consulta);
            if (err != 0)
            {
                printf("Error al insertar jugador %s en partida: %s\n", jugadores[i], mysql_error(conn));
            }
            else
            {
                printf("[BD] Jugador %s añadido a partida %d\n", jugadores[i], partida_id);
            }
        }
        else
        {
            printf("[BD] No se encontró ID para jugador: %s\n", jugadores[i]);
        }
    }

    return partida_id;
}

int actualizarPartidaFinalizada(MYSQL *conn, int partida_id, const char *ganador)
{
    char consulta[512];
    int ganador_id = -1;

    // Obtener el ID del ganador
    if (ganador && strlen(ganador) > 0)
    {
        ganador_id = obtenerIdJugadorPorNombre(conn, ganador);
    }

    // Actualizar la partida como finalizada
    if (ganador_id > 0)
    {
        snprintf(consulta, sizeof(consulta),
                 "UPDATE Partidas SET estado = 'FINALIZADA', fecha_fin = NOW(), ganador_id = %d "
                 "WHERE id_partida = %d",
                 ganador_id, partida_id);
    }
    else
    {
        snprintf(consulta, sizeof(consulta),
                 "UPDATE Partidas SET estado = 'FINALIZADA', fecha_fin = NOW() "
                 "WHERE id_partida = %d",
                 partida_id);
    }

    int err = mysql_query(conn, consulta);
    if (err != 0)
    {
        printf("Error al finalizar partida: %s\n", mysql_error(conn));
        return -1;
    }

    printf("[BD] Partida %d finalizada con ganador: %s\n", partida_id, ganador ? ganador : "sin ganador");
    return 0;
}

int actualizarEstadoPartida(MYSQL *conn, int partida_id, const char *estado)
{
    char consulta[256];

    snprintf(consulta, sizeof(consulta),
             "UPDATE Partidas SET estado = '%s' WHERE id_partida = %d",
             estado, partida_id);

    int err = mysql_query(conn, consulta);
    if (err != 0)
    {
        printf("Error al actualizar estado de partida: %s\n", mysql_error(conn));
        return -1;
    }

    printf("[BD] Estado de partida %d actualizado a: %s\n", partida_id, estado);
    return 0;
}

int actualizarRondaPartida(MYSQL *conn, int partida_id, int ronda, const char *carta_designada)
{
    char consulta[256];

    snprintf(consulta, sizeof(consulta),
             "UPDATE Partidas SET ronda_actual = %d, carta_designada = '%s' WHERE id_partida = %d",
             ronda, carta_designada, partida_id);

    int err = mysql_query(conn, consulta);
    if (err != 0)
    {
        printf("Error al actualizar ronda de partida: %s\n", mysql_error(conn));
        return -1;
    }

    printf("[BD] Partida %d - Ronda actualizada a %d, carta: %s\n", partida_id, ronda, carta_designada);
    return 0;
}

void listarPartidasCompletas(MYSQL *conn, char *lista, int tamano_lista)
{
    MYSQL_RES *res;
    MYSQL_ROW row;
    char consulta[512];

    lista[0] = '\0';

    // **CONSULTA MEJORADA QUE INCLUYE JUGADORES**
    snprintf(consulta, sizeof(consulta),
             "SELECT p.id_partida, DATE_FORMAT(p.fecha_inicio, '%%H:%%i'), "
             "COALESCE(j.nombre_usuario, 'En curso') as ganador, "
             "p.jugadores "
             "FROM Partidas p "
             "LEFT JOIN Jugadores j ON p.ganador_id = j.id_jugador "
             "ORDER BY p.fecha_inicio DESC LIMIT 20");

    int err = mysql_query(conn, consulta);
    if (err != 0)
    {
        printf("Error al consultar partidas: %s\n", mysql_error(conn));
        strcpy(lista, "ERROR/Error al consultar partidas");
        return;
    }

    res = mysql_store_result(conn);
    if (res)
    {
        strcat(lista, "LISTP/"); // Prefijo para identificar lista de partidas

        while ((row = mysql_fetch_row(res)))
        {
            char entrada[200];

            // **FORMATO: ID - Hora - Ganador - Jugadores**
            snprintf(entrada, sizeof(entrada), "Partida %s (%s) - %s - [%s]/",
                     row[0] ? row[0] : "?",              // id_partida
                     row[1] ? row[1] : "??:??",          // hora (HH:MM)
                     row[2] ? row[2] : "En curso",       // ganador
                     row[3] ? row[3] : "Sin jugadores"); // jugadores

            if (strlen(lista) + strlen(entrada) < (size_t)(tamano_lista - 50))
            {
                strcat(lista, entrada);
            }
            else
            {
                break;
            }
        }

        mysql_free_result(res);
        printf("[BD] Lista de partidas con jugadores enviada\n");
    }
    else
    {
        strcpy(lista, "LISTP/ERROR - No se pudieron obtener las partidas");
    }
}

void listarPartidasGanadas(MYSQL *conn, const char *nombre_usuario, char *lista, int tamano_lista)
{
    MYSQL_RES *res;
    MYSQL_ROW row;
    char consulta[512];
    int contador = 0;

    lista[0] = '\0';

    // **CONSULTA QUE INCLUYE JUGADORES**
    snprintf(consulta, sizeof(consulta),
             "SELECT p.id_partida, DATE_FORMAT(p.fecha_inicio, '%%H:%%i'), "
             "DATE_FORMAT(p.fecha_fin, '%%Y-%%m-%%d') as fecha, "
             "p.jugadores "
             "FROM Partidas p "
             "INNER JOIN Jugadores j ON p.ganador_id = j.id_jugador "
             "WHERE j.nombre_usuario = '%s' AND p.estado = 'FINALIZADA' "
             "ORDER BY p.fecha_fin DESC LIMIT 15",
             nombre_usuario);

    int err = mysql_query(conn, consulta);
    if (err != 0)
    {
        printf("Error al consultar partidas ganadas: %s\n", mysql_error(conn));
        strcpy(lista, "ERROR/Error al consultar partidas ganadas");
        return;
    }

    res = mysql_store_result(conn);
    if (res)
    {
        // **EMPEZAR CON PREFIJO**
        strcat(lista, "PARTIDASG/");

        while ((row = mysql_fetch_row(res)))
        {
            char entrada[200];

            // **FORMATO: Partida X - Fecha - Hora - Jugadores**
            snprintf(entrada, sizeof(entrada), "Partida %s - %s %s - [%s]/",
                     row[0] ? row[0] : "?",              // id_partida
                     row[2] ? row[2] : "Sin-fecha",      // fecha
                     row[1] ? row[1] : "Sin-hora",       // hora
                     row[3] ? row[3] : "Sin-jugadores"); // jugadores

            // Verificar que no se exceda el buffer
            if (strlen(lista) + strlen(entrada) < (size_t)(tamano_lista - 50))
            {
                strcat(lista, entrada);
                contador++;
            }
            else
            {
                break;
            }
        }

        // Si no hay partidas ganadas
        if (contador == 0)
        {
            strcat(lista, "No has ganado ninguna partida aun/");
        }

        mysql_free_result(res);
        printf("[BD] Lista de partidas ganadas para %s: %d entradas con jugadores\n", nombre_usuario, contador);
    }
    else
    {
        strcpy(lista, "PARTIDASG/ERROR - No se pudieron obtener las partidas ganadas");
    }
}

void listarPartidasConAmigo(MYSQL *conn, const char *usuario1, const char *usuario2, char *lista, int tamano_lista)
{
    MYSQL_RES *res;
    MYSQL_ROW row;
    char consulta[1024];
    int contador = 0;

    lista[0] = '\0';

    printf("[BD] Buscando partidas entre %s y %s\n", usuario1, usuario2);

    // **CONSULTA PARA BUSCAR PARTIDAS DONDE AMBOS USUARIOS PARTICIPARON**
    snprintf(consulta, sizeof(consulta),
             "SELECT p.id_partida, DATE_FORMAT(p.fecha_inicio, '%%Y-%%m-%%d %%H:%%i'), "
             "COALESCE(g.nombre_usuario, 'Sin ganador') as ganador, "
             "p.jugadores, p.estado "
             "FROM Partidas p "
             "LEFT JOIN Jugadores g ON p.ganador_id = g.id_jugador "
             "WHERE (p.jugadores LIKE '%%%s%%' AND p.jugadores LIKE '%%%s%%') "
             "ORDER BY p.fecha_inicio DESC LIMIT 20",
             usuario1, usuario2);

    int err = mysql_query(conn, consulta);
    if (err != 0)
    {
        printf("Error al consultar partidas con amigo: %s\n", mysql_error(conn));
        strcpy(lista, "ERROR/Error al consultar partidas");
        return;
    }

    res = mysql_store_result(conn);
    if (res)
    {
        // **EMPEZAR CON PREFIJO**
        strcat(lista, "PARTIDASAMIGO/");

        while ((row = mysql_fetch_row(res)))
        {
            char entrada[300];

            // **DETERMINAR RESULTADO PARA EL USUARIO1**
            const char *estado = row[4] ? row[4] : "ACTIVA";
            const char *ganador = row[2] ? row[2] : "Sin ganador";
            char resultado[50] = {0};

            if (strcmp(estado, "FINALIZADA") == 0)
            {
                if (strcmp(ganador, usuario1) == 0)
                {
                    strcpy(resultado, "GANASTE");
                }
                else if (strcmp(ganador, usuario2) == 0)
                {
                    strcpy(resultado, "PERDISTE");
                }
                else
                {
                    strcpy(resultado, "EMPATE");
                }
            }
            else
            {
                strcpy(resultado, estado);
            }

            // **FORMATO: Partida X - Fecha - Resultado - Jugadores**
            snprintf(entrada, sizeof(entrada), "Partida %s - %s - %s - [%s]/",
                     row[0] ? row[0] : "?",              // id_partida
                     row[1] ? row[1] : "Sin fecha",      // fecha y hora
                     resultado,                          // resultado
                     row[3] ? row[3] : "Sin jugadores"); // jugadores

            // Verificar que no se exceda el buffer
            if (strlen(lista) + strlen(entrada) < (size_t)(tamano_lista - 50))
            {
                strcat(lista, entrada);
                contador++;
            }
            else
            {
                break;
            }
        }

        // Si no hay partidas
        if (contador == 0)
        {
            char mensaje[200];
            snprintf(mensaje, sizeof(mensaje), "No hay partidas jugadas entre %s y %s/", usuario1, usuario2);
            strcat(lista, mensaje);
        }

        mysql_free_result(res);
        printf("[BD] Lista de partidas con amigo para %s y %s: %d entradas\n", usuario1, usuario2, contador);
    }
    else
    {
        strcpy(lista, "PARTIDASAMIGO/ERROR - No se pudieron obtener las partidas");
    }
}

// ñ
void listarConectados(MYSQL *conn, char *lista, int tamano_lista)
{
    MYSQL_RES *res;
    MYSQL_ROW row;
    char consulta[256];

    lista[0] = '\0';

    snprintf(consulta, sizeof(consulta), "SELECT nombre_usuario FROM Jugadores WHERE estado = 1 ORDER BY nombre_usuario");

    int err = mysql_query(conn, consulta);
    if (err != 0)
    {
        printf("Error al consultar jugadores conectados: %s\n", mysql_error(conn));
        return;
    }

    res = mysql_store_result(conn);
    if (res)
    {
        strcat(lista, "LIST/");
        while ((row = mysql_fetch_row(res)))
        {
            char entrada[100];
            snprintf(entrada, sizeof(entrada), "%s/", row[0]);

            if (strlen(lista) + strlen(entrada) < (size_t)(tamano_lista - 1))
            {
                strcat(lista, entrada);
            }
            else
            {
                break;
            }
        }
        mysql_free_result(res);
    }
    else
    {
        printf("Error al obtener resultado: %s\n", mysql_error(conn));
    }
}

void listarAmigos(MYSQL *conn, const char *usuario_solicitante, char *lista, int tamano_lista)
{
    MYSQL_RES *res;
    MYSQL_ROW row;
    char consulta[256];

    lista[0] = '\0';

    // **CONSULTA QUE EXCLUYE AL USUARIO SOLICITANTE**
    snprintf(consulta, sizeof(consulta),
             "SELECT nombre_usuario FROM Jugadores WHERE estado = 1 AND nombre_usuario != '%s' ORDER BY nombre_usuario",
             usuario_solicitante);

    int err = mysql_query(conn, consulta);
    if (err != 0)
    {
        printf("Error al consultar amigos: %s\n", mysql_error(conn));
        strcpy(lista, "LIST/Error al obtener lista de amigos");
        return;
    }

    res = mysql_store_result(conn);
    if (res)
    {
        strcat(lista, "LIST/");
        int contador = 0;

        while ((row = mysql_fetch_row(res)))
        {
            char entrada[100];
            snprintf(entrada, sizeof(entrada), "%s/", row[0]);

            if (strlen(lista) + strlen(entrada) < (size_t)(tamano_lista - 50))
            {
                strcat(lista, entrada);
                contador++;
            }
            else
            {
                break;
            }
        }

        // Si no hay otros usuarios conectados
        if (contador == 0)
        {
            strcat(lista, "No hay otros usuarios conectados/");
        }

        mysql_free_result(res);
        printf("[BD] Lista de amigos para %s: %d usuarios (excluido el solicitante)\n", usuario_solicitante, contador);
    }
    else
    {
        strcpy(lista, "LIST/Error al procesar lista de amigos");
    }
}
