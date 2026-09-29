# Limited database queries without an order give arbitrary results and EF Core warnings

EF Core logs "The query uses a row limiting operator ('Skip'/'Take') without an 'OrderBy'
operator" for some of our queries, and the results of those queries are not deterministic:

- The messenger's friend search (search by name prefix, capped by the configured search
  limit) returns an arbitrary subset of matching players in an arbitrary order when more
  players match than the limit allows. Players expect alphabetical results: ordered by name,
  with ties broken by player id.
- The navigator's room lookup by id is a limited query with no order either, which triggers
  the same warning on every lookup of uncached rooms (the rooms it returns must still come
  back in the order they were asked for).

Make every limited query involved here deterministic and warning-free.
