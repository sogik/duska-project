#include <mysql.h>
#include <string.h>
#include <stdlib.h>
#include <stdio.h>
#include <openssl/sha.h>
#include "basedatos.h"
#include "sistemainvitaciones.h"

ConexionJugador conexiones[MAX_CONEXIONES];
int num_conexiones = 0;
pthread_mutex_t mutex_conexiones = PTHREAD_MUTEX_INITIALIZER;

// Generación de hash SHA-256
/*void generarHashSHA256(const char* input, char* output) {
    unsigned char hash[SHA256_DIGEST_LENGTH];
    SHA256((const unsigned char*)input, strlen(input), hash);
    for(int i = 0; i < SHA256_DIGEST_LENGTH; i++) {
        sprintf(output + (i * 2), "%02x", hash[i]);
    }
    output[LONGITUD_HASH] = '\0';
}*/

int desconectar_base_datos(MYSQL *conn) {
    if(conn) mysql_close(conn);
    return BD_OK;
}

int usuarioExiste(MYSQL *conn, const char* nombre_usuario) {
    MYSQL_STMT *stmt = mysql_stmt_init(conn);
    const char *query = "SELECT nombre_usuario FROM Jugadores WHERE nombre_usuario = ?";
    
    if(mysql_stmt_prepare(stmt, query, strlen(query))) {
        fprintf(stderr, "Error preparando consulta: %s\n", mysql_stmt_error(stmt));
        mysql_stmt_close(stmt);
        return BD_ERROR;
    }

    MYSQL_BIND param = {0};
    param.buffer_type = MYSQL_TYPE_STRING;
    param.buffer = (char*)nombre_usuario;
    param.buffer_length = strlen(nombre_usuario);

    if(mysql_stmt_bind_param(stmt, &param)) {
        mysql_stmt_close(stmt);
        return BD_ERROR;
    }

    int existe = BD_OK;
    if(mysql_stmt_execute(stmt) == 0 && mysql_stmt_fetch(stmt) == 0) {
        existe = BD_USUARIO_EXISTE;
    }

    mysql_stmt_close(stmt);
    return existe;
}

int insertarUsuario(MYSQL *conn, const char* nombre_usuario, const char* contrasena) {
    char consulta[256];

    snprintf(consulta, sizeof(consulta), "INSERT INTO Jugadores (nombre_usuario, contrasena) VALUES ('%s', '%s')", nombre_usuario, contrasena);

    int err = mysql_query(conn, consulta);
    if (err != 0) {
        printf("Error al introducir datos en la base %u %s\n", mysql_errno(conn), mysql_error(conn));
        exit(1);
    }

    return 0;
}

int verificarCredenciales(MYSQL *conn, const char* nombre_usuario, const char* contrasena) {
    MYSQL_RES *res;
    MYSQL_ROW row;

    char consulta[256];

    snprintf(consulta, sizeof(consulta), "SELECT contrasena FROM Jugadores WHERE nombre_usuario = '%s'", nombre_usuario);

    int err = mysql_query(conn, consulta);
    if (err != 0) {
        printf("Error al consultar datos de la base %u %s\n", mysql_errno(conn), mysql_error(conn));
        exit(1);
    }

    res = mysql_store_result(conn);
    if (res) {
        row = mysql_fetch_row(res);
        mysql_free_result(res);

        if (row) {
            if (strcmp(row[0], contrasena) == 0) {
                return 0;
            }
        }
    }

    return 1;
}

void listarJugadores(MYSQL *conn, char *lista, int tamano_lista) {
    MYSQL_RES *res;
    MYSQL_ROW row;
    int espacio_usado = 0;

    lista[0] = '\0';
    
    if(mysql_query(conn, "SELECT id_jugador, nombre_usuario FROM Jugadores")) {
        return;
    }

    res = mysql_store_result(conn);
    if(res) {
        while((row = mysql_fetch_row(res)) && espacio_usado < tamano_lista) {
            int necesario = snprintf(lista + espacio_usado, tamano_lista - espacio_usado, 
                                   "%s\t%s\n", row[0], row[1]);
            if(necesario < 0) break;
            espacio_usado += necesario;
        }
        mysql_free_result(res);
    }
}

void listarPartidas(MYSQL *conn, char *lista, int tamano_lista) {
    MYSQL_RES *res;
    MYSQL_ROW row;
    int espacio_usado = 0;

    lista[0] = '\0';
    
    if(mysql_query(conn, "SELECT id_partida, fecha_inicio, duracion, ganador_id FROM Partidas")) {
        return;
    }

    res = mysql_store_result(conn);
    if(res) {
        while((row = mysql_fetch_row(res)) && espacio_usado < tamano_lista) {
            int necesario = snprintf(lista + espacio_usado, tamano_lista - espacio_usado,
                                   "%s\t%s\t%sm\t%s\n", row[0], row[1], row[2], row[3]);
            if(necesario < 0) break;
            espacio_usado += necesario;
        }
        mysql_free_result(res);
    }
}

void listarlistaconectados(MYSQL *conn, char *lista, int tamano_lista) {
    MYSQL_RES *res;
    MYSQL_ROW row;
    int espacio_usado = 0;

    lista[0] = '\0';
    
    if(mysql_query(conn, "SELECT id_jugador, nombre_usuario FROM Jugadores WHERE estado = 1")) {
        return;
    }

    res = mysql_store_result(conn);
    if(res) {
        while((row = mysql_fetch_row(res)) && espacio_usado < tamano_lista) {
            int necesario = snprintf(lista + espacio_usado, tamano_lista - espacio_usado,
                                   "%s\t%s\n", row[0], row[1]);
            if(necesario < 0) break;
            espacio_usado += necesario;
        }
        mysql_free_result(res);
    }
}
void listarPartidasGanadas(MYSQL *conn, const char* nombre_usuario, char *lista, int tamano_lista) {
    MYSQL_STMT *stmt = mysql_stmt_init(conn);
    const char *query = "SELECT p.id_partida, p.fecha_inicio, p.duracion FROM Partidas p "
                      "JOIN Jugadores j ON p.ganador_id = j.id_jugador "
                      "WHERE j.nombre_usuario = ?";
    
    if(mysql_stmt_prepare(stmt, query, strlen(query))) {
        return;
    }

    MYSQL_BIND param = {0};
    param.buffer_type = MYSQL_TYPE_STRING;
    param.buffer = (char*)nombre_usuario;
    param.buffer_length = strlen(nombre_usuario);

    if(mysql_stmt_bind_param(stmt, &param)) {
        mysql_stmt_close(stmt);
        return;
    }

    int espacio_usado = 0;
    lista[0] = '\0';
    
    if(mysql_stmt_execute(stmt) == 0) {
        MYSQL_BIND result[3];
        char id_partida[10], fecha[20], duracion[10];
        
        memset(result, 0, sizeof(result));
        result[0].buffer_type = MYSQL_TYPE_STRING;
        result[0].buffer = id_partida;
        result[0].buffer_length = sizeof(id_partida);
        
        result[1].buffer_type = MYSQL_TYPE_STRING;
        result[1].buffer = fecha;
        result[1].buffer_length = sizeof(fecha);
        
        result[2].buffer_type = MYSQL_TYPE_STRING;
        result[2].buffer = duracion;
        result[2].buffer_length = sizeof(duracion);

        if(mysql_stmt_bind_result(stmt, result) == 0) {
            while(mysql_stmt_fetch(stmt) == 0 && espacio_usado < tamano_lista) {
                int necesario = snprintf(lista + espacio_usado, tamano_lista - espacio_usado,
                                       "%s\t%s\t%s\n", id_partida, fecha, duracion);
                if(necesario < 0) break;
                espacio_usado += necesario;
            }
        }
    }
    
    mysql_stmt_close(stmt);
}

// Funciones de gestión de conexiones
int buscar_socket_por_id(int id_jugador) {
    pthread_mutex_lock(&mutex_conexiones);
    for(int i = 0; i < num_conexiones; i++) {
        if(conexiones[i].id_jugador == id_jugador) {
            pthread_mutex_unlock(&mutex_conexiones);
            return conexiones[i].socket;
        }
    }
    pthread_mutex_unlock(&mutex_conexiones);
    return -1;
}

void registrar_conexion(int id_jugador, int socket) {
    pthread_mutex_lock(&mutex_conexiones);
    for(int i = 0; i < num_conexiones; i++) {
        if(conexiones[i].id_jugador == id_jugador) {
            conexiones[i].socket = socket;
            pthread_mutex_unlock(&mutex_conexiones);
            return;
        }
    }
    if(num_conexiones < MAX_CONEXIONES) {
        conexiones[num_conexiones].id_jugador = id_jugador;
        conexiones[num_conexiones].socket = socket;
        num_conexiones++;
    }
    pthread_mutex_unlock(&mutex_conexiones);
}
