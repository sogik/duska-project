#ifndef PARTIDAS_H
#define PARTIDAS_H

// Estructura para mantener información de partidas
typedef struct GameInfo
{
    int partida_id;         // Identificador único de la partida
    int grupo_id;           // Grupo asociado a la partida
    char jugadores[10][50]; // Array de nombres de jugadores (máximo 10)
    int num_jugadores;      // Número total de jugadores
    int turno_actual;       // Índice del jugador con turno actual
    int partida_iniciada;   // Estado de la partida (0=no iniciada, 1=en curso)
    struct GameInfo *next;  // Para lista enlazada
} GameInfo;

// Funciones para gestión de partidas
int get_new_partida_id();
int crear_partida(int grupo_id);
int iniciar_partida(int partida_id);
GameInfo *obtener_partida_por_id(int partida_id);
GameInfo *obtener_partida_por_jugador(const char *usuario);
int es_turno_de_jugador(const char *usuario);
int avanzar_turno(int partida_id);
int finalizar_partida(int partida_id, const char *resultado);

// Función para procesar comandos relacionados con partidas y turnos
// Devuelve 1 si el comando fue manejado, 0 si no
int procesar_comando_partida(int codigo, const char *usuario, const char *contrasena,
                             const char *mensaje, char *respuesta, int respuesta_size);

#endif