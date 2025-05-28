#ifndef PARTIDAS_H
#define PARTIDAS_H

#define MAX_RONDAS 100

// Estructura para la información del juego
typedef struct GameInfo
{
    int partida_id;
    int grupo_id;
    int estado;
    int turno_actual;
    int num_jugadores;
    char **jugadores;
    struct GameInfo *next;

    int ronda_actual;
    int total_rondas;
    char cartas_ronda[MAX_RONDAS][10];

    char ultimo_jugador[50];
    int num_cartas_ultima_jugada;
    char cartas_ultima_jugada[10][5];
    int resultados_verificacion[10];

    int jugadores_eliminados[4]; // 1 si está eliminado
    int num_jugadores_activos;   // Contador de jugadores que siguen en la partida

    // Para manejar el proceso de eliminación en dos pasos
    int eliminacion_pendiente;              // 1 si hay una eliminación pendiente
    char jugador_pendiente_eliminacion[50]; // Nombre del jugador que será eliminado
} GameInfo;

// Lista global de partidas
extern GameInfo *partidas_lista;

// Prototipos de funciones
int crear_partida(int grupo_id);
int iniciar_partida(int partida_id);
int es_turno_de_jugador(const char *usuario);
GameInfo *obtener_partida_por_jugador(const char *usuario);
int avanzar_turno(int partida_id);
GameInfo *obtener_partida_por_id(int partida_id);
GameInfo *encontrar_partida(int partida_id);
int finalizar_partida(int partida_id);

// Prototipos de funciones para el sistema de rondas
int avanzar_ronda(int partida_id);
void generar_carta_para_ronda_actual(GameInfo *partida);
void guardar_ultima_jugada(GameInfo *partida, const char *jugador, char cartas[][5], int num_cartas);
void obtener_carta_ronda_actual(GameInfo *partida, char *carta_ronda);
int eliminar_jugador_de_partida(GameInfo *partida, char *jugador);

// Funciones externas necesarias
extern int obtener_grupo_id(const char *usuario);
extern int broadcast_to_group(int grupo_id, const char *mensaje);
extern char *obtener_usuario_grupo(int grupo_id, int indice);
extern int num_usuarios_grupo(int grupo_id);

#endif // PARTIDAS_H