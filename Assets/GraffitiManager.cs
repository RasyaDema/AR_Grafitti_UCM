using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class GraffitiManager : MonoBehaviour
{
    [Header("Wadah Prefab")]
    public GameObject prefabTeks;

    [Header("Elemen UI")]
    public GameObject grupTombolUtama;
    public GameObject grupTombolEdit;

    [Header("UI Bantuan")]
    public GameObject panelPetunjuk;
    public GameObject panelPetunjukTap;

    private int modeAktif = 0;
    private TextMeshPro objekTeksAktif;
    private TouchScreenKeyboard keyboard;

    private bool isDragging = false;
    private float jarakZ;
    private Vector3 offsetGeser;
    private string teksKetik = "";

    private Transform jangkarDunia;
    private Quaternion rotasiRelatifAwal;
    private bool tungguLepasTombol = false;

    void Start()
    {
        if (panelPetunjuk != null) panelPetunjuk.SetActive(true);
        if (panelPetunjukTap != null) panelPetunjukTap.SetActive(false);
        if (grupTombolUtama != null) grupTombolUtama.SetActive(false);
        if (grupTombolEdit != null) grupTombolEdit.SetActive(false);

        GameObject target = GameObject.Find("ImageTarget");
        if (target != null)
        {
            jangkarDunia = target.transform;

            // Opsional tapi disarankan: Pastikan Logo UC punya Collider raksasa
            // agar raycast tidak pernah meleset meski jari di luar batas visual logo
            if (target.GetComponent<Collider>() == null)
            {
                BoxCollider box = target.AddComponent<BoxCollider>();
                box.size = new Vector3(50f, 0.01f, 50f);
            }
        }
        else
        {
            jangkarDunia = new GameObject("JangkarTeksAR").transform;
        }
    }

    public void SaatLogoDitemukan()
    {
        if (panelPetunjuk != null) panelPetunjuk.SetActive(false);
        if (modeAktif == 0)
        {
            if (grupTombolUtama != null) grupTombolUtama.SetActive(true);
        }
    }

    public void SaatLogoHilang()
    {
        if (panelPetunjuk != null) panelPetunjuk.SetActive(true);
        if (grupTombolUtama != null) grupTombolUtama.SetActive(false);
        if (panelPetunjukTap != null) panelPetunjukTap.SetActive(false);
    }

    public void PilihModeTeks()
    {
        modeAktif = 2;
        tungguLepasTombol = true;
        if (grupTombolUtama != null) grupTombolUtama.SetActive(false);
        if (panelPetunjukTap != null) panelPetunjukTap.SetActive(true);
    }

    public void TekanTombolSave()
    {
        SelesaiEdit();
    }

    public void TekanTombolHapus()
    {
        if (objekTeksAktif != null)
        {
            Destroy(objekTeksAktif.gameObject);
            objekTeksAktif = null;
        }
        SelesaiEdit();
    }

    void Update()
    {
        if (tungguLepasTombol && Input.touchCount == 0 && !Input.GetMouseButton(0))
        {
            tungguLepasTombol = false;
        }

        if (TouchScreenKeyboard.isSupported && keyboard != null && objekTeksAktif != null)
        {
            teksKetik = keyboard.text;
            if (keyboard.status == TouchScreenKeyboard.Status.Done || keyboard.status == TouchScreenKeyboard.Status.Canceled)
            {
                keyboard.active = false;
            }
        }

#if UNITY_EDITOR
        if (objekTeksAktif != null && !isDragging)
        {
            foreach (char c in Input.inputString)
            {
                if (c == '\b')
                {
                    if (teksKetik.Length > 0) teksKetik = teksKetik.Substring(0, teksKetik.Length - 1);
                }
                else if (c == '\n' || c == '\r') { TekanTombolSave(); return; }
                else teksKetik += c;
            }
        }
#endif

        // VISUAL TEKS, ZOOM & ROTASI
        if (objekTeksAktif != null)
        {
            string kursor = (Time.time % 1f < 0.5f && grupTombolEdit.activeSelf) ? "|" : "";
            objekTeksAktif.text = teksKetik + kursor;

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0) objekTeksAktif.transform.localScale += Vector3.one * scroll * 0.5f;

            if (Input.touchCount == 2)
            {
                Touch t0 = Input.GetTouch(0);
                Touch t1 = Input.GetTouch(1);

                Vector2 t0Prev = t0.position - t0.deltaPosition;
                Vector2 t1Prev = t1.position - t1.deltaPosition;

                // Zoom
                float deltaMag = (t0.position - t1.position).magnitude - (t0Prev - t1Prev).magnitude;
                objekTeksAktif.transform.localScale += Vector3.one * (deltaMag * 0.002f);
                if (objekTeksAktif.transform.localScale.x < 0.01f)
                    objekTeksAktif.transform.localScale = Vector3.one * 0.01f;

                // Rotasi Bebas 2 Jari
                Vector2 arahLama = t1Prev - t0Prev;
                Vector2 arahBaru = t1.position - t0.position;
                float sudutPutar = Vector2.SignedAngle(arahLama, arahBaru);
                objekTeksAktif.transform.Rotate(0, 0, sudutPutar, Space.Self);

                rotasiRelatifAwal = objekTeksAktif.transform.rotation;
            }
        }

        // DETEKSI SENTUHAN (KLIK LAYAR)
        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current.IsPointerOverGameObject()) return;
            if (Input.touchCount > 0 && EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId)) return;
            if (Input.touchCount > 1) return;
            if (tungguLepasTombol) return;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            // A. Edit Teks Lama
            if (Physics.Raycast(ray, out hit))
            {
                TextMeshPro teksHit = hit.collider.gameObject.GetComponent<TextMeshPro>();
                if (teksHit != null)
                {
                    if (objekTeksAktif != null && objekTeksAktif != teksHit) SelesaiEdit();

                    objekTeksAktif = teksHit;
                    teksKetik = objekTeksAktif.text.Replace("|", "");
                    isDragging = true;
                    jarakZ = Camera.main.WorldToScreenPoint(objekTeksAktif.transform.position).z;

                    Vector3 posisiJariDunia = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, jarakZ));
                    offsetGeser = objekTeksAktif.transform.position - posisiJariDunia;
                    rotasiRelatifAwal = objekTeksAktif.transform.rotation;

                    AturUIEdit(true);
                    if (TouchScreenKeyboard.isSupported)
                        keyboard = TouchScreenKeyboard.Open(teksKetik, TouchScreenKeyboardType.Default);
                    return;
                }
            }

            // B. Buat Teks Baru
            if (modeAktif == 2 && prefabTeks != null && objekTeksAktif == null)
            {
                if (panelPetunjukTap != null) panelPetunjukTap.SetActive(false);

                // Buat Plane mengikuti lantai Logo UC
                Plane bidangLantai = new Plane(jangkarDunia.up, jangkarDunia.position);
                float jarakKeBidang;

                // Posisi cadangan jika raycast meleset
                Vector3 posisiMuncul = Camera.main.transform.position + Camera.main.transform.forward * 0.5f;

                if (bidangLantai.Raycast(ray, out jarakKeBidang))
                {
                    posisiMuncul = ray.GetPoint(jarakKeBidang);
                }

                // SOLUSI ROTASI TEGAK: Pastikan teks selalu menghadap kamera saat pertama kali dibuat,
                // bukannya miring mengikuti rotasi Logo UC.
                Quaternion rotasiMenghadapKamera = Quaternion.LookRotation(Camera.main.transform.forward, Camera.main.transform.up);

                GameObject teksBaru = Instantiate(prefabTeks, posisiMuncul, rotasiMenghadapKamera);

                // SOLUSI POSISI MENETAP: Teks WAJIB menjadi anak (child) dari ImageTarget (jangkarDunia).
                // Dengan ini, saat kamera melenceng dan kembali lagi, posisi teks akan ikut 
                // dikoreksi oleh Vuforia bersampingan dengan Logo UC-nya.
                teksBaru.transform.SetParent(jangkarDunia, true);

                objekTeksAktif = teksBaru.GetComponent<TextMeshPro>();
                teksKetik = "";
                isDragging = true;

                jarakZ = Camera.main.WorldToScreenPoint(posisiMuncul).z;
                offsetGeser = Vector3.zero;
                rotasiRelatifAwal = objekTeksAktif.transform.rotation;

                AturUIEdit(true);

                if (TouchScreenKeyboard.isSupported)
                    keyboard = TouchScreenKeyboard.Open("", TouchScreenKeyboardType.Default);
            }
        }

        // GESER TEKS
        if (Input.GetMouseButton(0) && isDragging && objekTeksAktif != null)
        {
            if (Input.touchCount <= 1)
            {
                Vector3 posisiLayar = new Vector3(Input.mousePosition.x, Input.mousePosition.y, jarakZ);
                objekTeksAktif.transform.position = Camera.main.ScreenToWorldPoint(posisiLayar) + offsetGeser;
                objekTeksAktif.transform.rotation = rotasiRelatifAwal;
            }
        }

        if (Input.GetMouseButtonUp(0)) isDragging = false;
    }

    void SelesaiEdit()
    {
        if (objekTeksAktif != null)
        {
            objekTeksAktif.text = teksKetik;
            if (string.IsNullOrWhiteSpace(teksKetik)) Destroy(objekTeksAktif.gameObject);
        }

        if (TouchScreenKeyboard.isSupported && keyboard != null) keyboard.active = false;

        keyboard = null;
        objekTeksAktif = null;
        teksKetik = "";
        isDragging = false;
        modeAktif = 0;
        tungguLepasTombol = false;

        if (panelPetunjukTap != null) panelPetunjukTap.SetActive(false);
        AturUIEdit(false);

        if (panelPetunjuk != null && !panelPetunjuk.activeSelf && grupTombolUtama != null)
        {
            grupTombolUtama.SetActive(true);
        }
    }

    void AturUIEdit(bool sedangEdit)
    {
        if (grupTombolUtama != null) grupTombolUtama.SetActive(!sedangEdit);
        if (grupTombolEdit != null) grupTombolEdit.SetActive(sedangEdit);
    }
}