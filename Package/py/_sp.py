import requests_html
from functools import lru_cache

SUBSENCE_HOST = 'https://subscene.com'

_session = requests_html.HTMLSession()
try:
    _post_url = SUBSENCE_HOST + _session.get(SUBSENCE_HOST).html.find('form', first=True).attrs['action']
except:
    _post_url = None


@lru_cache(maxsize=32)
def get_subtitles(href, limit):
    res = {}
    try:
        p = _session.get(SUBSENCE_HOST + href)
        sub = {}
        for link in p.html.find('a'):
            if link.attrs['href'].startswith(href):
                try:
                    span_lang = link.find('span', first=True)
                    lang = span_lang.text
                    status = span_lang.attrs['class'][-1]
                    if lang not in res:
                        res[lang] = {}
                    sub = {'status': status}
                    res[lang][link.attrs['href']] = sub
                except:
                    pass
            elif link.attrs['href'].startswith('/u/'):
                sub['user'] = link.text

        for k, v in res.items():
            links = sorted(v.items(), key=lambda l: l[1]['status'], reverse=True)
            res[k] = {l[0]: l[1].get('user', 'Anonymous') for l in links[0: limit]}

    except:
        raise
        pass

    return res


@lru_cache(maxsize=32)
def get_subtitle_data(year, title, limit):
    res = {}
    try:
        year = str(year)
        page = _session.post(_post_url, json={'query': title})
        exact = page.html.find('.exact', first=True)
        close = page.html.find('.close', first=True)
        if exact or close:
            for i, ul in enumerate(page.html.find('.search-result', first=True).find('ul')):
                for a in ul.find('a'):
                    if year in a.text:
                        if exact and i == 0:
                            href = a.attrs['href']
                            res = get_subtitles(href, limit)

                            if res:
                                return {'subtitles': res}
                        else:
                            res[a.text] = a.attrs['href']
            if res:
                return {'titles': res}
    except:
        raise
        pass

    return res


def get_subtitle(link):
    page = _session.get(SUBSENCE_HOST + link)
    response = _session.get(SUBSENCE_HOST + page.html.find('.download a', first=True).attrs['href'])
    return (response.headers.get('Content-Type'), response.content)
