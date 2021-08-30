import requests_html
from urllib.parse import quote
from functools import lru_cache

YTS_API = 'https://yts.mx/api/v2/list_movies.json'

_session = requests_html.HTMLSession()


@lru_cache(maxsize=32)
def get_movies(query):
    res = []
    try:
        url = '{}?{}'.format(YTS_API, query)
        movies = _session.get(url).json().get('data', {}).get('movies', [])
        res = [{
            'id': m.get('id'),
            'title': m.get('title'),
            'title_long': m.get('title_long'),
            'year': m.get('year'),
            'runtime': m.get('runtime'),
            'rating': m.get('rating'),
            'cover': m.get('medium_cover_image'),
            'background': m.get('background_image'),
            'genres': m.get('genres'),
            'summary': m.get('summary'),
            'trailer': m.get('yt_trailer_code'),
            'mpa_rating': m.get('mpa_rating'),
            'torrents': [{
                'url': t['url'],
                'hash': t['hash'],
                'quality': t['quality'],
                'type': t['type'],
            } for t in m.get('torrents', []) if t.get('quality') != '3D']
        } for m in movies]
    except:
        pass

    return res


def get_torrent_bytes(link):
    return _session.get(link).content
