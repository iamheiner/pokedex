CREATE TABLE pokedex_moves (
 id uuid PRIMARY KEY, name text NOT NULL CHECK (length(btrim(name)) > 0),
 power integer NOT NULL CHECK (power BETWEEN 1 AND 250),
 type integer NOT NULL CHECK (type BETWEEN 0 AND 17)
);
CREATE UNIQUE INDEX pokedex_moves_name ON pokedex_moves (upper(name));
CREATE TABLE pokedex_species (
 id uuid PRIMARY KEY, name text NOT NULL CHECK (length(btrim(name)) > 0),
 type integer NOT NULL CHECK (type BETWEEN 0 AND 17),
 health integer NOT NULL CHECK (health BETWEEN 1 AND 10000),
 attack integer NOT NULL CHECK (attack BETWEEN 1 AND 10000),
 defense integer NOT NULL CHECK (defense BETWEEN 1 AND 10000),
 special_attack integer NOT NULL CHECK (special_attack BETWEEN 1 AND 10000),
 special_defense integer NOT NULL CHECK (special_defense BETWEEN 1 AND 10000),
 speed integer NOT NULL CHECK (speed BETWEEN 1 AND 10000)
);
CREATE UNIQUE INDEX pokedex_species_name ON pokedex_species (upper(name));
CREATE TABLE pokedex_learnset (
 species_id uuid NOT NULL REFERENCES pokedex_species(id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED,
 move_id uuid NOT NULL REFERENCES pokedex_moves(id) DEFERRABLE INITIALLY DEFERRED,
 level integer NOT NULL CHECK (level BETWEEN 1 AND 100),
 PRIMARY KEY (species_id, move_id)
);
CREATE INDEX pokedex_learnset_move ON pokedex_learnset(move_id);
CREATE TABLE pokedex_pokemon (
 id uuid PRIMARY KEY,
 species_id uuid NOT NULL REFERENCES pokedex_species(id) DEFERRABLE INITIALLY DEFERRED,
 name text NOT NULL CHECK (length(btrim(name)) > 0),
 level integer NOT NULL CHECK (level BETWEEN 1 AND 100),
 current_health integer NOT NULL CHECK (current_health >= 0 AND current_health <= total_health),
 total_health integer NOT NULL CHECK (total_health BETWEEN 1 AND 10000)
);
CREATE INDEX pokedex_pokemon_species ON pokedex_pokemon(species_id);
CREATE TABLE pokedex_learned_moves (
 pokemon_id uuid NOT NULL REFERENCES pokedex_pokemon(id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED,
 slot integer NOT NULL CHECK (slot BETWEEN 0 AND 3),
 move_id uuid NOT NULL REFERENCES pokedex_moves(id) DEFERRABLE INITIALLY DEFERRED,
 PRIMARY KEY (pokemon_id, slot), UNIQUE (pokemon_id, move_id)
);
CREATE INDEX pokedex_learned_moves_move ON pokedex_learned_moves(move_id);
