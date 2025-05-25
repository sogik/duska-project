#ifndef PARTIDAS_H
#define PARTIDAS_H

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
int finalizar_partida(int partida_id);

// Funciones externas necesarias
extern int obtener_grupo_id(const char *usuario);
extern int broadcast_to_group(int grupo_id, const char *mensaje);
extern char *obtener_usuario_grupo(int grupo_id, int indice);
extern int num_usuarios_grupo(int grupo_id);

#endif // PARTIDAS_H