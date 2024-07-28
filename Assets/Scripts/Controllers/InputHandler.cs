using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputHandler : Singleton<InputHandler>
{
    [SerializeField] private PlayerInput playerInput;

    private enum ButtonIndices
    {
        Jump = 0,
        Sprint = 1,
        LockOn = 2,
        Slam = 3,
        Restart = 4,
        Pause = 5,
        Interact = 6,
        NUM_BUTTONS = 7
    }

    public Vector2 MoveXZ
    {
        get;
        private set;
    }
    public Vector2 Look
    {
        get;
        private set;
    }
    public Vector2 Scroll
    {
        get;
        private set;
    }
    public ButtonState Jump
    {
        get { return buttons[(int)ButtonIndices.Jump]; }
    }
    public ButtonState Sprint
    {
        get { return buttons[(int)ButtonIndices.Sprint]; }
    }
    public ButtonState LockOn
    {
        get { return buttons[(int)ButtonIndices.LockOn]; }
    }
    public ButtonState Slam
    {
        get { return buttons[(int)ButtonIndices.Slam]; }
    }
    public ButtonState Restart
    {
        get { return buttons[(int)ButtonIndices.Restart]; }
    }
    public ButtonState Pause
    {
        get { return buttons[(int)ButtonIndices.Pause]; }
    }
    public ButtonState Interact
    {
        get { return buttons[(int)ButtonIndices.Interact]; }
    }

    private int buttonCount = (int)ButtonIndices.NUM_BUTTONS;
    [SerializeField] private short bufferFrames = 5;
    [SerializeField] private bool bufferEnabled = false;
    private short IDSRC = 0;
    private ButtonState[] buttons;
    private Queue<Dictionary<short, short>> inputBuffer = new Queue<Dictionary<short, short>>();
    private Dictionary<short, short> currentFrame;

    public void Start()
    {
        buttons = new ButtonState[buttonCount];
        for (int i = 0; i < buttonCount; i++)
            buttons[i].Init(ref IDSRC, this);

        //if (SaveData.CurrSaveData.ReboundControls != null)
        //    playerInput.actions.LoadBindingOverridesFromJson(SaveData.CurrSaveData.ReboundControls);
    }

    private void FixedUpdate()
    {
        for (int i = 0; i < buttonCount; i++)
            buttons[i].Reset();

        if (bufferEnabled)
        {
            UpdateBuffer();
        }
    }

    //Input functions
    public void CTX_MoveXZ(InputAction.CallbackContext _ctx)
    {
        MoveXZ = _ctx.ReadValue<Vector2>();
    }
    public void CTX_Look(InputAction.CallbackContext _ctx)
    {
        Look = _ctx.ReadValue<Vector2>();
    }

    //Buttons
    public void CTX_Jump(InputAction.CallbackContext _ctx)
    {
        buttons[(int)ButtonIndices.Jump].Set(_ctx);
    }
    public void CTX_Sprint(InputAction.CallbackContext _ctx)
    {
        buttons[(int)ButtonIndices.Sprint].Set(_ctx);
    }
    public void CTX_LockOn(InputAction.CallbackContext _ctx)
    {
        buttons[(int)ButtonIndices.LockOn].Set(_ctx);
    }
    public void CTX_Slam(InputAction.CallbackContext _ctx)
    {
        buttons[(int)ButtonIndices.Slam].Set(_ctx);
    }
    public void CTX_Restart(InputAction.CallbackContext _ctx)
    {
        buttons[(int)ButtonIndices.Restart].Set(_ctx);
    }
    public void CTX_Pause(InputAction.CallbackContext _ctx)
    {
        buttons[(int)ButtonIndices.Pause].Set(_ctx);
    }
    public void CTX_Interact(InputAction.CallbackContext _ctx)
    {
        buttons[(int)ButtonIndices.Interact].Set(_ctx);
    }


    //Buffer functions
    public void FlushBuffer()
    {
        inputBuffer.Clear();
    }

    public void UpdateBuffer()
    {
        if (inputBuffer.Count >= bufferFrames)
            inputBuffer.Dequeue();
        currentFrame = new Dictionary<short, short>();
        inputBuffer.Enqueue(currentFrame);
    }

    public void PrintBuffer()
    {
        string bufferData = $"InputBuffer: count-{inputBuffer.Count}";
        foreach (var frame in inputBuffer)
            if (frame.Count > 0)
                bufferData += $"\n{frame.Count}";
        Debug.Log(bufferData);
    }

    public struct ButtonState
    {
        private short id;
        private static short STATE_PRESSED = 0,
                                STATE_RELEASED = 1;
        private InputHandler handler;
        private bool firstFrame;

        public bool Holding
        {
            get;
            private set;
        }
        public bool Down
        {
            get
            {
                if (handler.bufferEnabled && handler.inputBuffer != null)
                {
                    foreach (var frame in handler.inputBuffer)
                    {
                        if (frame.ContainsKey(id) && frame[id] == STATE_PRESSED)
                        {
                            return frame.Remove(id);
                        }
                    }
                    return false;
                }

                //Buffer disabled
                return (Holding && firstFrame);
            }
        }

        public bool Up
        {
            get
            {
                if (handler.bufferEnabled && handler.inputBuffer != null)
                {
                    foreach (var frame in handler.inputBuffer)
                    {
                        if (frame.ContainsKey(id) && frame[id] == STATE_RELEASED)
                        {
                            return frame.Remove(id);
                        }
                    }
                    return false;
                }

                //Buffer disabled
                return !Holding && firstFrame;
            }
        }

        public void Set(InputAction.CallbackContext ctx)
        {
            Holding = !ctx.canceled;
            firstFrame = true;

            if (handler.bufferEnabled && handler.currentFrame != null)
            {
                handler.currentFrame.TryAdd(id, Holding ? STATE_PRESSED : STATE_RELEASED);
            }
        }

        public void Reset()
        {
            firstFrame = false;
        }

        public void Init(ref short IDSRC, InputHandler handler)
        {
            id = IDSRC++;
            this.handler = handler;
        }
    }
}