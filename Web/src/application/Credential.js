import { useState } from "react";
import { atom, useAtom } from "jotai"
import { Box, Button, Dialog, FormControlLabel, LinearProgress, Switch, Typography } from "@material-ui/core";
import { Formik, Form, Field } from 'formik';
import { TextField } from 'formik-material-ui';
import { sha256 } from '../common/utils'
import { store } from "./diagram/Store";


const credentialAtom = atom(false)
const usernameKey = 'credential.userName'
let setShowFunc = null

export const showCredential = flag => setShowFunc?.(flag)

export const useCredential = () => {
    const [show, setShow] = useAtom(credentialAtom)
    if (setShowFunc == null) {
        setShowFunc = setShow
    }
    return [show, setShow]
}


export default function Credential() {
    const [show, setShow] = useCredential()
    const [createAccount, setCreateAccount] = useState(false)
    const userName = localStorage.getItem(usernameKey) ?? ''

    return (
        <Dialog open={show} onClose={() => { }}  >
            <Box style={{ width: 320, height: 330, padding: 20 }}>

                {!createAccount && <Typography variant="h5" style={{ paddingBottom: 10 }} >Login</Typography>}
                {createAccount && <Typography variant="h5" style={{ paddingBottom: 10 }} >Create an Account</Typography>}

                <Formik
                    initialValues={{
                        username: userName,
                        password: '',
                        confirm: '',
                        create: false
                    }}

                    validate={async values => {
                        const errors = {};
                        if (!values.username) {
                            errors.username = 'Required';
                        }
                        if (!values.password) {
                            errors.password = 'Required';
                        }
                        if (createAccount && values.password !== values.confirm) {
                            errors.confirm = 'Does not match password'
                        }
                        return errors;
                    }}

                    onSubmit={async (values, { setErrors, setFieldValue }) => {
                        // Reduce risk of clair text password logging
                        const password = await sha256(values.password)
                        if (createAccount) {
                            try {
                                await store.createUser({ username: values.username, password: password })
                                localStorage.setItem(usernameKey, values.username)
                                setCreateAccount(false)
                                setFieldValue('confirm', '', false)
                            } catch (error) {
                                setFieldValue('password', '', false)
                                setFieldValue('confirm', '', false)
                                setErrors({ username: 'User already exist' })
                                return
                            }
                        }

                        try {
                            await store.connectUser({ username: values.username, password: password })
                            localStorage.setItem(usernameKey, values.username)
                        } catch (error) {
                            console.log('error', error)
                            const msg = 'Invalid username or password'
                            setFieldValue('password', '', false)
                            setErrors({ username: msg })
                        }
                    }}
                >

                    {({ submitForm, isSubmitting }) => (
                        <Form>
                            {isSubmitting && <LinearProgress style={{ marginBottom: 10 }} />}
                            <Field
                                component={TextField}
                                name="username"
                                type="text"
                                label="Username"
                                fullWidth={true}
                            />
                            <br />
                            <Field
                                component={TextField}
                                type="password"
                                label="Password"
                                name="password"
                                fullWidth={true}
                            />
                            <br />
                            {createAccount && <Field
                                component={TextField}
                                type="password"
                                label="Confirm"
                                name="confirm"
                                fullWidth={true}
                            />}

                            <br />
                            <FormControlLabel style={{ position: 'absolute', bottom: 70 }}
                                control={
                                    <Field component={Switch} type="checkbox" name="create" color='primary'
                                        onChange={e => setCreateAccount(e.target.checked)} />
                                }
                                label="Create an account"
                            />
                            <br />


                            <Box style={{ position: 'absolute', bottom: 15, left: 80 }}>
                                <Button
                                    variant="contained"
                                    color="primary"
                                    disabled={isSubmitting}
                                    onClick={submitForm}
                                    style={{ margin: 5, width: 80 }}
                                >OK</Button>

                                <Button
                                    variant="contained"
                                    color="primary"
                                    disabled={isSubmitting}
                                    onClick={() => setShow(false)}
                                    style={{ margin: 5, width: 85 }}
                                >Cancel</Button>
                            </Box>

                        </Form>
                    )}
                </Formik>
            </Box>
        </Dialog >
    )
}